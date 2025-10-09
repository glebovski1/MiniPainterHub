using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.ErrorHandling;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.OpenAPIOperationFilter;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Images;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ------------------------------------------------------------------
        // 1️⃣  Services
        // ------------------------------------------------------------------
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                sqlOpts => sqlOpts.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)
                                   .MigrationsHistoryTable("__EFMigrationsHistory", "dbo")));

        builder.Services.AddDefaultIdentity<ApplicationUser>(o =>
        {
            o.SignIn.RequireConfirmedAccount = false;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager();

        // JWT
        var jwt = builder.Configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwt["Key"]!);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwt["Audience"],
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuerSigningKey = true
            };
        });

        builder.Services.AddControllers();

        builder.Services.AddProblemDetails(o =>
        {
            o.CustomizeProblemDetails = ctx =>
            {
                ctx.ProblemDetails.Extensions["requestId"] = ctx.HttpContext.TraceIdentifier;
            };
        });
        builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "MiniPainterHub API", Version = "v1" });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter ‘Bearer {token}’"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = Array.Empty<string>()
            });
            c.MapType<IFormFile>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "binary"
            });
            c.MapType<IEnumerable<IFormFile>>(() => new OpenApiSchema
            {
                Type = "array",
                Items = new OpenApiSchema { Type = "string", Format = "binary" }
            });

        });

        builder.Services.AddOptions<ImagesOptions>()
            .Bind(builder.Configuration.GetSection("Images"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddScoped<IImageProcessor, ImageProcessor>();

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSingleton<LocalImageService>();
            builder.Services.AddSingleton<IImageService>(sp => sp.GetRequiredService<LocalImageService>());
            builder.Services.AddSingleton<IImageStore>(sp => sp.GetRequiredService<LocalImageService>());
        }
        else
        {
            builder.Services.AddSingleton(provider =>
            {
                var config = provider.GetRequiredService<IConfiguration>();
                var connection = config["ImageStorage:AzureConnectionString"]
                    ?? throw new InvalidOperationException("Azure connection string is not configured.");
                var containerName = config["ImageStorage:AzureContainer"]
                    ?? throw new InvalidOperationException("Azure container name is not configured.");

                var container = new BlobContainerClient(connection, containerName);
                container.CreateIfNotExists();
                return container;
            });

            builder.Services.AddSingleton<AzureBlobImageService>();
            builder.Services.AddSingleton<IImageService>(sp => sp.GetRequiredService<AzureBlobImageService>());
            builder.Services.AddSingleton<IImageStore>(sp => sp.GetRequiredService<AzureBlobImageService>());
        }

        builder.Services.AddScoped<IProfileService, ProfileService>();
        builder.Services.AddScoped<IPostService, PostService>();
        builder.Services.AddScoped<ICommentService, CommentService>();
        builder.Services.AddScoped<ILikeService, LikeService>();
        builder.Services.AddAuthorization();

        var app = builder.Build();

        // ------------------------------------------------------------------
        // 2️⃣  Seed test data
        // ------------------------------------------------------------------
        if (app.Environment.IsDevelopment())
        {
            await DataSeeder.SeedAsync(app.Services);
        }

        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        if (app.Environment.IsProduction())
        {
            if (app.Environment.IsProduction())
            {
                try
                {
                    await using var scope = app.Services.CreateAsyncScope();
                    // DB already migrated, so this should be a no-op; keep it for safety:
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.Database.MigrateAsync();
                    logger.LogInformation("EF migrations ok.");

                    // Seed admin may be your crash point—keep it but log it:
                    await DataSeeder.SeedAdminAsync(app);
                    logger.LogInformation("Admin seed ok.");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Startup failed in Production.");
                    throw;
                }
            }
        }

        // ------------------------------------------------------------------
        // 3️⃣  HTTP pipeline
        // ------------------------------------------------------------------
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MiniPainterHub API v1");
            });
        }
        else
        {
            app.UseHsts();
        }

        app.UseExceptionHandler();

        app.UseHttpsRedirection();
        app.UseBlazorFrameworkFiles();  // 🟡 Serve WASM framework files
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.MapFallbackToFile("index.html"); // 🟢 Route everything else to Blazor

        app.MapGet("/healthz", () => Results.Ok("OK"));

        

        app.Run();
    }
}

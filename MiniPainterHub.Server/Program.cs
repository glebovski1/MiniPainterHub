using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.OpenAPIOperationFilter;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using System;
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

            c.OperationFilter<FileUploadOperationFilter>();
        });

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSingleton<IImageService, LocalImageService>();
        }
        else
        {
            builder.Services.AddSingleton<IImageService, AzureBlobImageService>();
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

        // ------------------------------------------------------------------
        // 3️⃣  HTTP pipeline
        // ------------------------------------------------------------------
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseWebAssemblyDebugging();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MiniPainterHub API v1");
            });
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseBlazorFrameworkFiles();  // 🟡 Serve WASM framework files
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.MapFallbackToFile("index.html"); // 🟢 Route everything else to Blazor

        app.Run();
    }
}

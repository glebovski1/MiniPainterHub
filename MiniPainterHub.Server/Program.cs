using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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
using MiniPainterHub.Server.Realtime;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Images;
using MiniPainterHub.Server.Services.Interfaces;
using System.Net;
using System.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var developmentCommand = TryParseDevelopmentCommand(args);

        // ------------------------------------------------------------------
        // 1️⃣  Services
        // ------------------------------------------------------------------
        var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
        var useInMemoryDatabase =
            builder.Environment.IsDevelopment()
            && OperatingSystem.IsLinux()
            && defaultConnection?.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) == true;

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            if (useInMemoryDatabase)
            {
                options.UseInMemoryDatabase("MiniPainterHubDev");
                return;
            }

            options.UseSqlServer(
                defaultConnection,
                sqlOpts => sqlOpts.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)
                                   .MigrationsHistoryTable("__EFMigrationsHistory", "dbo"));
        });

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
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs/chat"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddControllers();
        builder.Services.AddSignalR();

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
        builder.Services.AddScoped<DevelopmentContentSeeder>();
        builder.Services.AddScoped<IAccountRestrictionService, AccountRestrictionService>();
        builder.Services.AddScoped<IModerationService, ModerationService>();
        builder.Services.AddScoped<IFollowService, FollowService>();
        builder.Services.AddScoped<IConversationService, ConversationService>();
        builder.Services.AddSingleton<IChatNotifier, SignalRChatNotifier>();
        builder.Services.AddAuthorization();

        var app = builder.Build();

        if (developmentCommand is not null)
        {
            await RunDevelopmentCommandAsync(app, developmentCommand);
            return;
        }

        // ------------------------------------------------------------------
        // 2️⃣  Seed test data
        // ------------------------------------------------------------------
        if (app.Environment.IsDevelopment())
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            if (db.Database.IsRelational())
            {
                await EnsureDevelopmentDatabaseAsync(db, startupLogger, app.Configuration);
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }

            await DataSeeder.SeedAsync(app.Services);
        }

        var logger = app.Services.GetRequiredService<ILogger<Program>>();

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

        if (app.Environment.IsDevelopment())
        {
            var localImageStorage = LocalImageStoragePaths.Resolve(app.Environment, app.Configuration);
            Directory.CreateDirectory(localImageStorage.PhysicalPath);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(localImageStorage.PhysicalPath),
                RequestPath = localImageStorage.RequestPath
            });
        }

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<ChatHub>("/hubs/chat");

        app.MapFallbackToFile("index.html"); // 🟢 Route everything else to Blazor

        app.MapGet("/healthz", () => Results.Ok("OK"));

        var resetToken = app.Configuration["TestSupport:ResetToken"];
        var resetEnabled = app.Configuration.GetValue<bool>("TestSupport:ResetEnabled");
        if (app.Environment.IsDevelopment() && resetEnabled && !string.IsNullOrWhiteSpace(resetToken))
        {
            app.MapPost("/api/test-support/reset", async (HttpContext context, AppDbContext db) =>
            {
                var requestToken = context.Request.Headers["X-Test-Support-Token"].ToString();
                if (string.IsNullOrWhiteSpace(requestToken))
                {
                    return Results.Unauthorized();
                }

                var remoteIp = context.Connection.RemoteIpAddress;
                if (remoteIp is null)
                {
                    return Results.Forbid();
                }

                if (remoteIp.IsIPv4MappedToIPv6)
                {
                    remoteIp = remoteIp.MapToIPv4();
                }

                if (!IPAddress.IsLoopback(remoteIp))
                {
                    return Results.Forbid();
                }

                var expectedTokenBytes = Encoding.UTF8.GetBytes(resetToken);
                var requestTokenBytes = Encoding.UTF8.GetBytes(requestToken);
                var validToken = CryptographicOperations.FixedTimeEquals(expectedTokenBytes, requestTokenBytes);
                if (!validToken)
                {
                    return Results.Unauthorized();
                }

                await db.Database.EnsureDeletedAsync();
                if (db.Database.IsRelational())
                {
                    await db.Database.MigrateAsync();
                }
                else
                {
                    await db.Database.EnsureCreatedAsync();
                }
                await DataSeeder.SeedAsync(app.Services);

                return Results.Ok(new { ok = true });
            });
        }


        app.Run();
    }

    private static async Task EnsureDevelopmentDatabaseAsync(AppDbContext db, ILogger logger, IConfiguration configuration)
    {
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (SqlException ex) when (IsDuplicateIdentitySchemaError(ex) && ShouldRecreateOnSchemaConflict(configuration))
        {
            logger.LogWarning(
                ex,
                "Detected schema conflict while applying migrations in Development. Recreating database and retrying migration.");
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }
    }

    private static bool IsDuplicateIdentitySchemaError(SqlException ex) =>
        ex.Number == 2714 && ex.Message.Contains("AspNetRoles", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldRecreateOnSchemaConflict(IConfiguration configuration) =>
        configuration.GetValue<bool?>("Database:RecreateOnSchemaConflict") ?? true;

    private static DevelopmentCommand? TryParseDevelopmentCommand(string[] args)
    {
        var seedDevContent = args.Contains("--seed-dev-content", StringComparer.OrdinalIgnoreCase);
        var generateDevAvatars = args.Contains("--generate-dev-avatars", StringComparer.OrdinalIgnoreCase);

        if (!seedDevContent && !generateDevAvatars)
        {
            return null;
        }

        if (seedDevContent && generateDevAvatars)
        {
            throw new InvalidOperationException("Specify either --seed-dev-content or --generate-dev-avatars, not both.");
        }

        string? avatarsDirectory = null;
        string? postImagesDirectory = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--avatars-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("The --avatars-dir option requires a value.");
                }

                avatarsDirectory = args[i + 1];
                i++;
                continue;
            }

            if (string.Equals(args[i], "--post-images-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("The --post-images-dir option requires a value.");
                }

                postImagesDirectory = args[i + 1];
                i++;
            }
        }

        if (string.IsNullOrWhiteSpace(avatarsDirectory))
        {
            var commandName = seedDevContent ? "--seed-dev-content" : "--generate-dev-avatars";
            throw new InvalidOperationException($"The {commandName} command requires --avatars-dir <path>.");
        }

        if (generateDevAvatars && !string.IsNullOrWhiteSpace(postImagesDirectory))
        {
            throw new InvalidOperationException("The --generate-dev-avatars command does not support --post-images-dir.");
        }

        var commandKind = seedDevContent
            ? DevelopmentCommandKind.SeedContent
            : DevelopmentCommandKind.GenerateAvatarsOnly;

        return new DevelopmentCommand(commandKind, avatarsDirectory, postImagesDirectory);
    }

    private static async Task RunDevelopmentCommandAsync(WebApplication app, DevelopmentCommand command)
    {
        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException($"The {GetCommandName(command.Kind)} command can only run in Development.");
        }

        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentContentSeeder>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        switch (command.Kind)
        {
            case DevelopmentCommandKind.SeedContent:
            {
                var result = await seeder.ResetAndSeedAsync(command.AvatarsDirectory, command.PostImagesDirectory);

                logger.LogInformation(
                    "Development seed complete. Users: {Users}, posts: {Posts}, comments: {Comments}, avatars: {Avatars}, post images: {PostImages}.",
                    result.UsersCreated,
                    result.PostsCreated,
                    result.CommentsCreated,
                    result.AvatarsImported,
                    result.PostImagesImported);

                foreach (var credential in result.Credentials)
                {
                    logger.LogInformation(
                        "Seeded user {UserName} ({Email}) roles [{Roles}] password {Password}",
                        credential.UserName,
                        credential.Email,
                        string.Join(", ", credential.Roles),
                        credential.Password);
                }

                break;
            }
            case DevelopmentCommandKind.GenerateAvatarsOnly:
            {
                var result = await seeder.GenerateAvatarsOnlyAsync(command.AvatarsDirectory);

                logger.LogInformation(
                    "Development avatar generation complete. Avatars: {Avatars}, existing users updated: {UsersUpdated}.",
                    result.AvatarsImported,
                    result.ExistingUsersUpdated);

                foreach (var avatar in result.Avatars)
                {
                    logger.LogInformation(
                        "Prepared avatar for {UserName} from {SourceFile} at {AvatarUrl}",
                        avatar.UserName,
                        avatar.SourceFileName,
                        avatar.AvatarUrl);
                }

                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported development command '{command.Kind}'.");
        }
    }

    private static string GetCommandName(DevelopmentCommandKind kind) => kind switch
    {
        DevelopmentCommandKind.SeedContent => "--seed-dev-content",
        DevelopmentCommandKind.GenerateAvatarsOnly => "--generate-dev-avatars",
        _ => throw new InvalidOperationException($"Unsupported development command '{kind}'.")
    };

    private enum DevelopmentCommandKind
    {
        SeedContent,
        GenerateAvatarsOnly
    }

    private sealed record DevelopmentCommand(DevelopmentCommandKind Kind, string AvatarsDirectory, string? PostImagesDirectory);
}

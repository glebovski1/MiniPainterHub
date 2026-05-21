using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
using MiniPainterHub.Server.Middleware;
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;

namespace MiniPainterHub;

public class Program
{
    internal const string LighthouseEnvironmentName = "Lighthouse";
    internal const int SqlServerMaxRetryCount = 6;
    internal static readonly TimeSpan SqlServerMaxRetryDelay = TimeSpan.FromSeconds(10);
    private const int StaticAssetOneYearSeconds = 31_536_000;
    private const int StaticAssetOneWeekSeconds = 604_800;
    private const int StaticAssetOneDaySeconds = 86_400;

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var developmentCommand = TryParseDevelopmentCommand(args);
        builder.Configuration
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
            .AddJsonFile(
                $"appsettings.Local.{builder.Environment.EnvironmentName}.json",
                optional: true,
                reloadOnChange: true);

        var isLocalToolingEnvironment = IsLocalToolingEnvironment(builder.Environment);
        var hostedStartupConfiguration = isLocalToolingEnvironment
            ? null
            : HostedStartupConfigurationValidator.Validate(builder.Configuration, builder.Environment.EnvironmentName);

        // ------------------------------------------------------------------
        // 1️⃣  Services
        // ------------------------------------------------------------------
        var configuredConnection = hostedStartupConfiguration?.DefaultConnectionString
            ?? builder.Configuration.GetConnectionString("DefaultConnection");
        var connectionResolution = ResolveDevelopmentConnectionString(builder.Environment, builder.Configuration, configuredConnection);
        var defaultConnection = connectionResolution.ConnectionString;
        var useInMemoryDatabase =
            isLocalToolingEnvironment
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
                ConfigureSqlServerOptions);
        });

        builder.Services.AddDefaultIdentity<ApplicationUser>(o =>
        {
            o.SignIn.RequireConfirmedAccount = false;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager();

        // JWT
        var jwt = hostedStartupConfiguration?.Jwt;
        var key = Encoding.UTF8.GetBytes(jwt?.Key ?? builder.Configuration["Jwt:Key"]!);

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
                ValidIssuer = jwt?.Issuer ?? builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = jwt?.Audience ?? builder.Configuration["Jwt:Audience"],
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

        builder.Services.AddDataProtection();
        builder.Services.AddControllers();
        builder.Services.AddSignalR();
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/javascript",
                "application/octet-stream",
                "application/wasm",
                "image/svg+xml"
            });
        });
        builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

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
        builder.Services.AddOptions<MaintenanceOptions>()
            .Bind(builder.Configuration.GetSection("Maintenance"));

        builder.Services.AddScoped<IImageProcessor, ImageProcessor>();

        if (isLocalToolingEnvironment)
        {
            builder.Services.AddSingleton<LocalImageService>();
            builder.Services.AddSingleton<IImageService>(sp => sp.GetRequiredService<LocalImageService>());
            builder.Services.AddSingleton<IImageStore>(sp => sp.GetRequiredService<LocalImageService>());
        }
        else
        {
            var azureBlobStorage = hostedStartupConfiguration!.AzureBlobStorage;
            builder.Services.AddSingleton(_ =>
            {
                var container = new BlobContainerClient(
                    azureBlobStorage.ConnectionString,
                    azureBlobStorage.ContainerName);
                container.CreateIfNotExists();
                return container;
            });

            builder.Services.AddSingleton<AzureBlobImageService>();
            builder.Services.AddSingleton<IImageService>(sp => sp.GetRequiredService<AzureBlobImageService>());
            builder.Services.AddSingleton<IImageStore>(sp => sp.GetRequiredService<AzureBlobImageService>());
        }

        builder.Services.AddScoped<IProfileService, ProfileService>();
        builder.Services.AddScoped<IPostService, PostService>();
        builder.Services.AddScoped<IPostViewerService, PostViewerService>();
        builder.Services.AddScoped<ICommentService, CommentService>();
        builder.Services.AddScoped<CommentMarkService>();
        builder.Services.AddScoped<ICommentMarkService>(sp => sp.GetRequiredService<CommentMarkService>());
        builder.Services.AddScoped<IAuthorMarkService, AuthorMarkService>();
        builder.Services.AddScoped<ILikeService, LikeService>();
        builder.Services.AddScoped<DevelopmentContentSeeder>();
        builder.Services.AddScoped<IAccountRestrictionService, AccountRestrictionService>();
        builder.Services.AddScoped<IModerationService, ModerationService>();
        builder.Services.AddScoped<ISearchService, SearchService>();
        builder.Services.AddScoped<IReportService, ReportService>();
        builder.Services.AddScoped<IFollowService, FollowService>();
        builder.Services.AddScoped<IConversationService, ConversationService>();
        builder.Services.AddSingleton<IMaintenanceBypassService, MaintenanceBypassService>();
        builder.Services.AddSingleton<IChatNotifier, SignalRChatNotifier>();
        builder.Services.AddAuthorization();

        var app = builder.Build();

        if (connectionResolution.ResolutionMessage is not null)
        {
            app.Logger.LogWarning("{Message}", connectionResolution.ResolutionMessage);
        }

        if (developmentCommand is not null)
        {
            await RunDevelopmentCommandAsync(app, developmentCommand);
            return;
        }

        // ------------------------------------------------------------------
        // 2️⃣  Seed test data
        // ------------------------------------------------------------------
        if (IsLocalToolingEnvironment(app.Environment))
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            LogDatabaseTarget(db, startupLogger);
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
        if (hostedStartupConfiguration is not null)
        {
            HostedStartupConfigurationValidator.LogSummary(
                logger,
                hostedStartupConfiguration,
                app.Environment.EnvironmentName);
        }

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
        app.UseResponseCompression();
        UseStaticAssetHeaderPolicy(app);
        app.UseAuthentication();
        app.UseMiddleware<MaintenanceModeMiddleware>();
        UsePublishedBootManifestStaticFile(app);
        app.UseBlazorFrameworkFiles();  // 🟡 Serve WASM framework files

        if (IsLocalToolingEnvironment(app.Environment))
        {
            var localImageStorage = LocalImageStoragePaths.Resolve(app.Environment, app.Configuration);
            Directory.CreateDirectory(localImageStorage.PhysicalPath);
            app.UseStaticFiles(CreateStaticFileOptions(
                new PhysicalFileProvider(localImageStorage.PhysicalPath),
                localImageStorage.RequestPath));
        }

        app.UseStaticFiles(CreateStaticFileOptions());
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<ChatHub>("/hubs/chat");

        app.MapFallbackToFile("index.html"); // 🟢 Route everything else to Blazor

        app.MapGet("/healthz", () => Results.Ok("OK"));

        var resetToken = app.Configuration["TestSupport:ResetToken"];
        var resetEnabled = app.Configuration.GetValue<bool>("TestSupport:ResetEnabled");
        if (IsLocalToolingEnvironment(app.Environment) && resetEnabled && !string.IsNullOrWhiteSpace(resetToken))
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

    private static void UsePublishedBootManifestStaticFile(WebApplication app)
    {
        if (string.IsNullOrWhiteSpace(app.Environment.WebRootPath))
        {
            return;
        }

        var bootManifestPath = Path.Combine(app.Environment.WebRootPath, "_framework", "blazor.boot.json");
        if (!File.Exists(bootManifestPath))
        {
            return;
        }

        app.Use(async (context, next) =>
        {
            if (!string.Equals(
                    context.Request.Path.Value,
                    "/_framework/blazor.boot.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            context.Response.ContentType = "application/json";
            ApplyStaticAssetHeaders(context);
            await context.Response.SendFileAsync(bootManifestPath);
        });
    }

    private static StaticFileOptions CreateStaticFileOptions() =>
        new()
        {
            OnPrepareResponse = context => ApplyStaticAssetHeaders(context.Context)
        };

    private static StaticFileOptions CreateStaticFileOptions(IFileProvider fileProvider, PathString requestPath) =>
        new()
        {
            FileProvider = fileProvider,
            RequestPath = requestPath,
            OnPrepareResponse = context => ApplyStaticAssetHeaders(context.Context)
        };

    private static void UseStaticAssetHeaderPolicy(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (app.Environment.IsProduction() && IsPortableDebugSymbol(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.OnStarting(() =>
            {
                ApplySecurityHeaders(context);
                ApplyApiResponseHeaders(context);
                ApplyStaticAssetHeaders(context);
                return Task.CompletedTask;
            });

            await next();
        });
    }

    private static void ApplyStaticAssetHeaders(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!IsManagedStaticAssetPath(path))
        {
            return;
        }

        var cacheControl = ResolveStaticAssetCacheControl(path);
        if (!string.IsNullOrWhiteSpace(cacheControl))
        {
            context.Response.Headers["Cache-Control"] = cacheControl;
        }

        ApplySecurityHeaders(context);
    }

    private static void ApplySecurityHeaders(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    private static void ApplyApiResponseHeaders(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["Pragma"] = "no-cache";
    }

    private static string ResolveStaticAssetCacheControl(string path)
    {
        var fileName = GetRequestFileName(path);
        if (IsAlwaysRevalidatedAsset(path, fileName))
        {
            return "no-cache";
        }

        if (HasFingerprintInFileName(fileName))
        {
            return $"public, max-age={StaticAssetOneYearSeconds}, immutable";
        }

        if (path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase))
        {
            return $"public, max-age={StaticAssetOneWeekSeconds}";
        }

        if (IsImageAsset(path))
        {
            return $"public, max-age={StaticAssetOneWeekSeconds}";
        }

        if (IsFontAsset(path) || IsCssOrScriptAsset(path))
        {
            return $"public, max-age={StaticAssetOneDaySeconds}";
        }

        return $"public, max-age={StaticAssetOneDaySeconds}";
    }

    private static bool IsManagedStaticAssetPath(string path) =>
        path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/JSHelpers/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase)
        || IsKnownRootStaticAsset(path);

    private static bool IsKnownRootStaticAsset(string path)
    {
        var fileName = GetRequestFileName(path);
        return string.Equals(path, "/", StringComparison.Ordinal)
            || string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "favicon.png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "manifest.webmanifest", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "service-worker.js", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "service-worker-assets.js", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "MiniPainterHub.WebApp.styles.css", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "appsettings.client.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAlwaysRevalidatedAsset(string path, string fileName) =>
        string.Equals(path, "/", StringComparison.Ordinal)
        || string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, "blazor.boot.json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, "service-worker.js", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, "service-worker-assets.js", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, "appsettings.client.json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, "manifest.webmanifest", StringComparison.OrdinalIgnoreCase);

    private static bool IsPortableDebugSymbol(PathString path) =>
        path.Value?.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) == true;

    private static string GetRequestFileName(string path)
    {
        var queryStart = path.IndexOf('?', StringComparison.Ordinal);
        var pathOnly = queryStart >= 0 ? path[..queryStart] : path;
        var lastSlash = pathOnly.LastIndexOf('/');
        return lastSlash >= 0 ? pathOnly[(lastSlash + 1)..] : pathOnly;
    }

    private static bool IsCssOrScriptAsset(string path) =>
        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase);

    private static bool IsFontAsset(string path) =>
        path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase);

    private static bool IsImageAsset(string path) =>
        path.EndsWith(".avif", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);

    private static bool HasFingerprintInFileName(string fileName)
    {
        var tokenLength = 0;
        for (var index = 0; index <= fileName.Length; index++)
        {
            var current = index < fileName.Length ? fileName[index] : '.';
            if (IsHexDigit(current))
            {
                tokenLength++;
                continue;
            }

            if (tokenLength >= 8)
            {
                return true;
            }

            tokenLength = 0;
        }

        return false;
    }

    private static bool IsHexDigit(char value) =>
        value is >= '0' and <= '9'
        || value is >= 'a' and <= 'f'
        || value is >= 'A' and <= 'F';

    internal static void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder sqlOpts)
    {
        sqlOpts.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)
            .MigrationsHistoryTable("__EFMigrationsHistory", "dbo")
            .EnableRetryOnFailure(
                maxRetryCount: SqlServerMaxRetryCount,
                maxRetryDelay: SqlServerMaxRetryDelay,
                errorNumbersToAdd: null);
    }

    internal static bool IsLocalToolingEnvironment(IHostEnvironment environment) =>
        environment.IsDevelopment()
        || environment.IsEnvironment(LighthouseEnvironmentName);

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

    private static ConnectionResolution ResolveDevelopmentConnectionString(
        IHostEnvironment environment,
        IConfiguration configuration,
        string? configuredConnection)
    {
        if (!IsLocalToolingEnvironment(environment)
            || !OperatingSystem.IsWindows()
            || string.IsNullOrWhiteSpace(configuredConnection)
            || !IsLocalDbConnection(configuredConnection))
        {
            return new ConnectionResolution(configuredConnection, null);
        }

        if (CanOpenSqlConnection(BuildProbeConnectionString(configuredConnection)))
        {
            return new ConnectionResolution(configuredConnection, null);
        }

        if (TryStartLocalDbInstance(configuredConnection, out var localDbStartMessage)
            && CanOpenSqlConnection(BuildProbeConnectionString(configuredConnection)))
        {
            return new ConnectionResolution(
                configuredConnection,
                localDbStartMessage);
        }

        if (AllowSqlExpressFallbackInDevelopment(configuration))
        {
            var sqlExpressConnection = TryCreateSqlExpressFallbackConnectionString(configuredConnection);
            if (sqlExpressConnection is not null && CanOpenSqlConnection(BuildProbeConnectionString(sqlExpressConnection)))
            {
                return new ConnectionResolution(
                    sqlExpressConnection,
                    "Configured LocalDB instance was unavailable. Falling back to .\\SQLEXPRESS for Development.");
            }
        }

        return new ConnectionResolution(
            configuredConnection,
            "Configured LocalDB instance was unavailable. Development will keep using LocalDB so your existing MiniPainterHub data stays on the expected database.");
    }

    private static bool AllowSqlExpressFallbackInDevelopment(IConfiguration configuration) =>
        configuration.GetValue<bool>("Database:AllowSqlExpressFallbackInDevelopment");

    private static bool IsLocalDbConnection(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.DataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string BuildProbeConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
            ConnectTimeout = 3,
            Pooling = false
        };

        builder.AttachDBFilename = string.Empty;

        return builder.ConnectionString;
    }

    private static bool TryStartLocalDbInstance(string connectionString, out string message)
    {
        message = string.Empty;

        var instanceName = TryGetLocalDbInstanceName(connectionString);
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sqllocaldb",
                Arguments = $"start {instanceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                message = $"Timed out while starting LocalDB instance '{instanceName}'.";
                return false;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            if (process.ExitCode != 0)
            {
                message = string.IsNullOrWhiteSpace(error)
                    ? $"Failed to start LocalDB instance '{instanceName}'."
                    : $"Failed to start LocalDB instance '{instanceName}': {error}";
                return false;
            }

            message = string.IsNullOrWhiteSpace(output)
                ? $"Started LocalDB instance '{instanceName}' for Development."
                : $"Started LocalDB instance '{instanceName}' for Development: {output}";
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            message = $"Failed to launch sqllocaldb for '{instanceName}': {ex.Message}";
            return false;
        }
    }

    private static string? TryGetLocalDbInstanceName(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var dataSource = builder.DataSource;
            if (!dataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return dataSource[(dataSource.IndexOf('\\') + 1)..];
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? TryCreateSqlExpressFallbackConnectionString(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                DataSource = @".\SQLEXPRESS",
                ConnectTimeout = 3,
                TrustServerCertificate = true
            };

            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                builder.InitialCatalog = "MiniPainterHub";
            }

            builder.AttachDBFilename = string.Empty;

            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool CanOpenSqlConnection(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void LogDatabaseTarget(AppDbContext db, ILogger logger)
    {
        var providerName = db.Database.ProviderName ?? "unknown";

        if (!db.Database.IsRelational())
        {
            logger.LogInformation("Using database provider {Provider}.", providerName);
            return;
        }

        var connection = db.Database.GetDbConnection();
        logger.LogInformation(
            "Using database provider {Provider}. DataSource={DataSource}; Database={Database}",
            providerName,
            connection.DataSource,
            connection.Database);
    }

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

    private sealed record ConnectionResolution(string? ConnectionString, string? ResolutionMessage);
}

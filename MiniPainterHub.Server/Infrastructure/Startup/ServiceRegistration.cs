using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.ErrorHandling;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Realtime;
using MiniPainterHub.Server.Features.Posts;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Images;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace MiniPainterHub;

public partial class Program
{
    private static ServiceRegistrationResult ConfigureMiniPainterHubServices(WebApplicationBuilder builder)
    {
        var isLocalToolingEnvironment = IsLocalToolingEnvironment(builder.Environment);
        var hostedStartupConfiguration = isLocalToolingEnvironment
            ? null
            : HostedStartupConfigurationValidator.Validate(builder.Configuration, builder.Environment.EnvironmentName);

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
            if (isLocalToolingEnvironment)
            {
                options.ConfigureWarnings(warnings =>
                    warnings.Log(RelationalEventId.PendingModelChangesWarning));
            }

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

        AddJwtAuthentication(builder, hostedStartupConfiguration?.Jwt);
        AddFrameworkServices(builder);
        AddOptions(builder);
        AddImageServices(builder, isLocalToolingEnvironment, hostedStartupConfiguration);
        AddDomainServices(builder.Services);

        builder.Services.AddAuthorization();

        return new ServiceRegistrationResult(hostedStartupConfiguration, connectionResolution);
    }

    private static void AddJwtAuthentication(WebApplicationBuilder builder, JwtStartupConfiguration? jwt)
    {
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

                    return System.Threading.Tasks.Task.CompletedTask;
                }
            };
        });
    }

    private static void AddFrameworkServices(WebApplicationBuilder builder)
    {
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
                Description = "Enter 'Bearer {token}'"
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
    }

    private static void AddOptions(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<ImagesOptions>()
            .Bind(builder.Configuration.GetSection("Images"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddOptions<MaintenanceOptions>()
            .Bind(builder.Configuration.GetSection("Maintenance"));
    }

    private static void AddImageServices(
        WebApplicationBuilder builder,
        bool isLocalToolingEnvironment,
        HostedStartupConfiguration? hostedStartupConfiguration)
    {
        builder.Services.AddScoped<IImageProcessor, ImageProcessor>();

        if (isLocalToolingEnvironment)
        {
            builder.Services.AddSingleton<LocalImageService>();
            builder.Services.AddSingleton<IImageService>(sp => sp.GetRequiredService<LocalImageService>());
            builder.Services.AddSingleton<IImageStore>(sp => sp.GetRequiredService<LocalImageService>());
            return;
        }

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

    private static void AddDomainServices(IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IPostImageAttachmentService, PostImageAttachmentService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IPostViewerService, PostViewerService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<CommentMarkService>();
        services.AddScoped<ICommentMarkService>(sp => sp.GetRequiredService<CommentMarkService>());
        services.AddScoped<IAuthorMarkService, AuthorMarkService>();
        services.AddScoped<ILikeService, LikeService>();
        services.AddScoped<DevelopmentContentSeeder>();
        services.AddScoped<IAccountRestrictionService, AccountRestrictionService>();
        services.AddScoped<IModerationService, ModerationService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IFollowService, FollowService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddSingleton<IMaintenanceBypassService, MaintenanceBypassService>();
        services.AddSingleton<IChatNotifier, SignalRChatNotifier>();
    }

    private sealed record ServiceRegistrationResult(
        HostedStartupConfiguration? HostedStartupConfiguration,
        ConnectionResolution ConnectionResolution);
}

using Azure.Storage.Blobs;
using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.ErrorHandling;
using MiniPainterHub.Server.Infrastructure.Caching;
using MiniPainterHub.Server.Infrastructure.Database;
using MiniPainterHub.Server.Infrastructure.RateLimiting;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Realtime;
using MiniPainterHub.Server.Features.Posts;
using MiniPainterHub.Server.Features.Seo;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Images;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

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

        builder.Services.AddScoped<RequestDatabaseMetrics>();
        builder.Services.AddScoped<DatabaseCommandMetricsInterceptor>();
        builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            ConfigureRelationalWarnings(options);
            options.AddInterceptors(serviceProvider.GetRequiredService<DatabaseCommandMetricsInterceptor>());

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
            o.SignIn.RequireConfirmedEmail = builder.Configuration.GetValue<bool>("EmailConfirmation:Enabled");
            o.User.RequireUniqueEmail = true;
            o.Lockout.AllowedForNewUsers = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager();
        builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(24));

        AddAuthentication(builder, hostedStartupConfiguration?.Jwt);
        AddFrameworkServices(builder);
        AddOptions(builder);
        AddEmailServices(builder);
        AddTrafficShaping(builder);
        AddImageServices(builder, isLocalToolingEnvironment, hostedStartupConfiguration);
        AddDomainServices(builder.Services);

        builder.Services.AddAuthorization();

        return new ServiceRegistrationResult(hostedStartupConfiguration, connectionResolution);
    }

    internal static void ConfigureRelationalWarnings(DbContextOptionsBuilder options)
    {
        options.ConfigureWarnings(warnings =>
            warnings.Log(RelationalEventId.PendingModelChangesWarning));
    }

    private static void AddAuthentication(WebApplicationBuilder builder, JwtStartupConfiguration? jwt)
    {
        var key = Encoding.UTF8.GetBytes(jwt?.Key ?? builder.Configuration["Jwt:Key"]!);

        var authentication = builder.Services.AddAuthentication(options =>
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
        })
        .AddCookie(ExternalAuthenticationSchemes.ExternalCookie, options =>
        {
            options.Cookie.Name = "mph.external-provider";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = IsLocalToolingEnvironment(builder.Environment)
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            options.SlidingExpiration = false;
        });

        var google = builder.Configuration.GetSection(GoogleAuthenticationOptions.SectionName).Get<GoogleAuthenticationOptions>()
            ?? new GoogleAuthenticationOptions();
        authentication.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, FakeGoogleAuthenticationHandler>(
            ExternalAuthenticationSchemes.FakeGoogle,
            _ => { });

        if (google.Enabled && !google.UseFakeProvider)
        {
            authentication.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = google.ClientId!;
                options.ClientSecret = google.ClientSecret!;
                options.CallbackPath = google.CallbackPath;
                options.SignInScheme = ExternalAuthenticationSchemes.ExternalCookie;
                options.SaveTokens = false;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.ClaimActions.Add(new JsonKeyClaimAction(
                    "urn:google:email_verified",
                    ClaimValueTypes.Boolean,
                    "email_verified"));
                options.Events.OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    context.Response.Redirect("/api/auth/google/complete?error=cancelled");
                    return System.Threading.Tasks.Task.CompletedTask;
                };
            });
        }

        var discord = builder.Configuration.GetSection(DiscordAuthenticationOptions.SectionName).Get<DiscordAuthenticationOptions>()
            ?? new DiscordAuthenticationOptions();
        authentication.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, FakeDiscordAuthenticationHandler>(
            ExternalAuthenticationSchemes.FakeDiscord,
            _ => { });

        if (discord.Enabled && !discord.UseFakeProvider)
        {
            authentication.AddOAuth(ExternalAuthenticationSchemes.Discord, options =>
            {
                options.ClientId = discord.ClientId!;
                options.ClientSecret = discord.ClientSecret!;
                options.CallbackPath = discord.CallbackPath;
                options.SignInScheme = ExternalAuthenticationSchemes.ExternalCookie;
                options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
                options.TokenEndpoint = "https://discord.com/api/oauth2/token";
                options.UserInformationEndpoint = "https://discord.com/api/v10/users/@me";
                options.SaveTokens = false;
                options.Scope.Clear();
                options.Scope.Add("identify");
                options.Scope.Add("email");
                options.ClaimActions.Add(new JsonKeyClaimAction(
                    ClaimTypes.NameIdentifier,
                    ClaimValueTypes.String,
                    "id"));
                options.ClaimActions.Add(new JsonKeyClaimAction(
                    ClaimTypes.Email,
                    ClaimValueTypes.Email,
                    "email"));
                options.ClaimActions.Add(new JsonKeyClaimAction(
                    ClaimTypes.Name,
                    ClaimValueTypes.String,
                    "global_name"));
                options.ClaimActions.Add(new JsonKeyClaimAction(
                    "urn:discord:username",
                    ClaimValueTypes.String,
                    "username"));
                options.ClaimActions.Add(new JsonKeyClaimAction(
                    "urn:discord:email_verified",
                    ClaimValueTypes.Boolean,
                    "verified"));
                options.ClaimActions.Add(new JsonKeyClaimAction(
                    "urn:discord:bot",
                    ClaimValueTypes.Boolean,
                    "bot"));
                options.ClaimActions.Add(new JsonKeyClaimAction(
                    "urn:discord:system",
                    ClaimValueTypes.Boolean,
                    "system"));
                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.AccessToken))
                        {
                            throw new InvalidOperationException("Discord did not provide an access token.");
                        }

                        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        request.Headers.UserAgent.ParseAdd("MiniPainterHub/1.0");
                        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                        response.EnsureSuccessStatusCode();
                        await using var responseStream = await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted);
                        using var user = await JsonDocument.ParseAsync(
                            responseStream,
                            cancellationToken: context.HttpContext.RequestAborted);
                        context.RunClaimActions(user.RootElement);
                    },
                    OnRemoteFailure = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect("/api/auth/discord/complete?error=cancelled");
                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                };
            });
        }
    }

    private static void AddFrameworkServices(WebApplicationBuilder builder)
    {
        var dataProtection = builder.Services.AddDataProtection()
            .SetApplicationName("MiniPainterHub");
        if (!IsLocalToolingEnvironment(builder.Environment))
        {
            var keyPath = builder.Configuration["DataProtection:KeysPath"]
                ?? Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? builder.Environment.ContentRootPath, "data-protection-keys");
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        }

        ConfigureForwardedHeaders(builder);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMemoryCache();
        var publicCacheSeconds = Math.Clamp(
            builder.Configuration.GetValue<int?>("DatabasePerformance:PublicCacheSeconds") ?? 10,
            1,
            60);
        builder.Services.AddOutputCache(options =>
        {
            options.AddPolicy(OutputCachePolicies.PublicDatabaseShort, policy => policy
                .Expire(TimeSpan.FromSeconds(publicCacheSeconds))
                .SetVaryByQuery("*")
                .Tag("public-database"));
        });
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
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Roll & Paint API", Version = "v1" });

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
        builder.Services.AddOptions<DatabasePerformanceOptions>()
            .Bind(builder.Configuration.GetSection(DatabasePerformanceOptions.SectionName));
        builder.Services.AddSingleton<IValidateOptions<SiteBrandOptions>, SiteBrandOptionsValidator>();
        builder.Services.AddOptions<SiteBrandOptions>()
            .Bind(builder.Configuration.GetSection(SiteBrandOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddOptions<ImagesOptions>()
            .Bind(builder.Configuration.GetSection("Images"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddOptions<MaintenanceOptions>()
            .Bind(builder.Configuration.GetSection("Maintenance"));
        builder.Services.AddOptions<TrafficShapingOptions>()
            .Bind(builder.Configuration.GetSection("TrafficShaping"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<EmailConfirmationOptions>, EmailConfirmationOptionsValidator>();
        builder.Services.AddOptions<EmailConfirmationOptions>()
            .Bind(builder.Configuration.GetSection(EmailConfirmationOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<GoogleAuthenticationOptions>, GoogleAuthenticationOptionsValidator>();
        builder.Services.AddOptions<GoogleAuthenticationOptions>()
            .Bind(builder.Configuration.GetSection(GoogleAuthenticationOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<DiscordAuthenticationOptions>, DiscordAuthenticationOptionsValidator>();
        builder.Services.AddOptions<DiscordAuthenticationOptions>()
            .Bind(builder.Configuration.GetSection(DiscordAuthenticationOptions.SectionName))
            .ValidateOnStart();
    }

    private static void ConfigureForwardedHeaders(WebApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
        {
            return;
        }

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;

            if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustAllProxies"))
            {
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                return;
            }

            foreach (var value in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
            {
                if (IPAddress.TryParse(value, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }
        });
    }

    private static void AddTrafficShaping(WebApplicationBuilder builder)
    {
        var traffic = builder.Configuration.GetSection("TrafficShaping").Get<TrafficShapingOptions>() ?? new TrafficShapingOptions();

        builder.Services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                var problemDetails = new ProblemDetails
                {
                    Title = "Too many requests",
                    Detail = "Roll & Paint is receiving too many requests from this client. Please wait a moment and try again.",
                    Status = StatusCodes.Status429TooManyRequests,
                    Instance = httpContext.Request.Path
                };

                var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails = problemDetails
                });
            };

            options.AddPolicy(RateLimitingPolicies.Auth, context =>
                CreateFixedWindowPartition(GetIpPartitionKey(context), traffic.Auth));
            options.AddPolicy(RateLimitingPolicies.EmailConfirmation, context =>
                CreateFixedWindowPartition(GetIpPartitionKey(context), traffic.EmailConfirmation));
            options.AddPolicy(RateLimitingPolicies.Search, context =>
                CreateFixedWindowPartition(GetIpPartitionKey(context), traffic.Search));
            options.AddPolicy(RateLimitingPolicies.Write, context =>
                CreateFixedWindowPartition(GetUserOrIpPartitionKey(context), traffic.Write));
            options.AddPolicy(RateLimitingPolicies.Upload, context =>
                CreateFixedWindowPartition(GetUserOrIpPartitionKey(context), traffic.Upload));
            options.AddPolicy(RateLimitingPolicies.Realtime, context =>
                CreateFixedWindowPartition(GetIpPartitionKey(context), traffic.Realtime));
        });
    }

    private static RateLimitPartition<string> CreateFixedWindowPartition(string partitionKey, RateLimitPolicyOptions policy) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = Math.Max(1, policy.PermitLimit),
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(Math.Max(1, policy.WindowSeconds))
            });

    private static string GetUserOrIpPartitionKey(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userId)
            ? GetIpPartitionKey(context)
            : "user:" + userId;
    }

    private static string GetIpPartitionKey(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return "ip:unknown";
        }

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        return "ip:" + remoteIp;
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

    private static void AddEmailServices(WebApplicationBuilder builder)
    {
        var email = builder.Configuration.GetSection(EmailConfirmationOptions.SectionName).Get<EmailConfirmationOptions>()
            ?? new EmailConfirmationOptions();

        if (!email.Enabled)
        {
            builder.Services.AddSingleton<IAccountEmailSender, DisabledAccountEmailSender>();
        }
        else if (string.Equals(email.Provider, EmailConfirmationProviders.AzureCommunicationServices, StringComparison.Ordinal))
        {
            builder.Services.AddSingleton(_ => new EmailClient(new Uri(email.Endpoint!), new DefaultAzureCredential()));
            builder.Services.AddSingleton<IAccountEmailSender, AzureCommunicationAccountEmailSender>();
        }
        else
        {
            builder.Services.AddSingleton<IAccountEmailSender, DevelopmentAccountEmailSender>();
        }

        builder.Services.AddScoped<IEmailConfirmationService, EmailConfirmationService>();
    }

    private static void AddDomainServices(IServiceCollection services)
    {
        services.AddScoped<PublicSeoDocumentService>();
        services.AddSingleton<PublicSeoShellRenderer>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IPostImageAttachmentService, PostImageAttachmentService>();
        services.AddScoped<HobbyProjectService>();
        services.AddScoped<IHobbyProjectService>(sp => sp.GetRequiredService<HobbyProjectService>());
        services.AddScoped<IHobbyProjectPostLinker>(sp => sp.GetRequiredService<HobbyProjectService>());
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IPostViewerService, PostViewerService>();
        services.AddScoped<IPaintingGuideService, PaintingGuideService>();
        services.AddScoped<INewsAnnouncementService, NewsAnnouncementService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<CommentMarkService>();
        services.AddScoped<ICommentMarkService>(sp => sp.GetRequiredService<CommentMarkService>());
        services.AddScoped<IAuthorMarkService, AuthorMarkService>();
        services.AddScoped<ILikeService, LikeService>();
        services.AddScoped<DevelopmentContentSeeder>();
        services.AddScoped<IAccountRestrictionService, AccountRestrictionService>();
        services.AddScoped<IModerationService, ModerationService>();
        services.AddScoped<IAdminInboxService, AdminInboxService>();
        services.AddScoped<IAdminSiteControlService, AdminSiteControlService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IFollowService, FollowService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<ISupportTicketService, SupportTicketService>();
        services.AddScoped<IJwtTokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IExternalAuthenticationService, ExternalAuthenticationService>();
        services.AddSingleton<IUploadConcurrencyLimiter, UploadConcurrencyLimiter>();
        services.AddSingleton<ISiteActivityTracker, SiteActivityTracker>();
        services.AddSingleton<IMaintenanceBypassService, MaintenanceBypassService>();
        services.AddSingleton<IChatNotifier, SignalRChatNotifier>();
    }

    private sealed record ServiceRegistrationResult(
        HostedStartupConfiguration? HostedStartupConfiguration,
        ConnectionResolution ConnectionResolution);
}

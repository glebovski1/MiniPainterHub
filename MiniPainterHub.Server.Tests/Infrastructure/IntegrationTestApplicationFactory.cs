using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MiniPainterHub;
using MiniPainterHub.Server.Data;

namespace MiniPainterHub.Server.Tests.Infrastructure;

public sealed class IntegrationTestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"tests-{Guid.NewGuid():N}";
    private readonly IReadOnlyDictionary<string, string?> _overrides;
    private readonly IPAddress? _forcedRemoteIp;
    private readonly string _imageRoot;

    public IntegrationTestApplicationFactory(
        IDictionary<string, string?>? configurationOverrides = null,
        IPAddress? forcedRemoteIp = null)
    {
        _imageRoot = Path.Combine(Path.GetTempPath(), "MiniPainterHub.Tests", _databaseName, "uploads", "images");
        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ImageStorage:LocalPath"] = _imageRoot,
            ["ImageStorage:RequestPath"] = "/uploads/images"
        };

        if (configurationOverrides is not null)
        {
            foreach (var pair in configurationOverrides)
            {
                overrides[pair.Key] = pair.Value;
            }
        }

        _overrides = overrides;
        _forcedRemoteIp = forcedRemoteIp;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            if (_overrides.Count > 0)
            {
                config.AddInMemoryCollection(_overrides);
            }
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthorizationOptions>(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(_forcedRemoteIp));
        });
    }

    public HttpClient CreateAuthenticatedClient(
        string userId = TestAuthHandler.DefaultUserId,
        string userName = TestAuthHandler.DefaultUserName,
        string role = TestAuthHandler.DefaultRole)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserNameHeader, userName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        return client;
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        if (Directory.Exists(_imageRoot))
        {
            Directory.Delete(_imageRoot, recursive: true);
        }

        Directory.CreateDirectory(_imageRoot);
    }

    public async Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }
}

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-UserId";
    public const string UserNameHeader = "X-Test-UserName";
    public const string RoleHeader = "X-Test-Role";

    public const string DefaultUserId = "test-user";
    public const string DefaultUserName = "tester";
    public const string DefaultRole = "User";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith(SchemeName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers[UserIdHeader].ToString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = DefaultUserId;
        }

        var userName = Request.Headers[UserNameHeader].ToString();
        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = DefaultUserName;
        }

        var role = Request.Headers[RoleHeader].ToString();
        if (string.IsNullOrWhiteSpace(role))
        {
            role = DefaultRole;
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

internal sealed class RemoteIpStartupFilter : IStartupFilter
{
    private readonly IPAddress? _forcedRemoteIp;

    public RemoteIpStartupFilter(IPAddress? forcedRemoteIp)
    {
        _forcedRemoteIp = forcedRemoteIp;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use((context, pipelineNext) =>
            {
                if (_forcedRemoteIp is not null)
                {
                    context.Connection.RemoteIpAddress = _forcedRemoteIp;
                }

                return pipelineNext();
            });

            next(app);
        };
    }
}

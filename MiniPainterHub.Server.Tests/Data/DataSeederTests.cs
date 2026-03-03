using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Data;

public class DataSeederTests
{
    [Fact]
    public async Task SeedAsync_WhenCalledTwice_IsIdempotent()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();

        await DataSeeder.SeedAsync(factory.Services);
        await DataSeeder.SeedAsync(factory.Services);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Users.Should().HaveCount(2);
        db.Roles.Should().HaveCount(2);
        (await roles.RoleExistsAsync("Admin")).Should().BeTrue();
        (await roles.RoleExistsAsync("User")).Should().BeTrue();

        var admin = await users.FindByEmailAsync("admin@local");
        admin.Should().NotBeNull();
        (await users.IsInRoleAsync(admin!, "Admin")).Should().BeTrue();
        (await users.IsInRoleAsync(admin!, "User")).Should().BeTrue();

        var normalUser = await users.FindByEmailAsync("user@local");
        normalUser.Should().NotBeNull();
        (await users.IsInRoleAsync(normalUser!, "User")).Should().BeTrue();
    }

    [Fact]
    public async Task SeedAdminAsync_WhenDisabled_DoesNothing()
    {
        await using var app = await BuildSeedAdminApp(new Dictionary<string, string?>
        {
            ["SeedAdmin:Enabled"] = "false",
            ["SeedAdmin:Email"] = "admin@example.test",
            ["SeedAdmin:Password"] = "Admin123!",
            ["SeedAdmin:Role"] = "Admin"
        });

        await DataSeeder.SeedAdminAsync(app);

        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        (await users.FindByEmailAsync("admin@example.test")).Should().BeNull();
        (await roles.RoleExistsAsync("Admin")).Should().BeFalse();
    }

    private static async Task<WebApplication> BuildSeedAdminApp(IDictionary<string, string?> configuration)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
            ApplicationName = typeof(DataSeederTests).Assembly.FullName
        });

        builder.Configuration.AddInMemoryCollection(configuration);

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"seed-admin-tests-{Guid.NewGuid():N}"));

        builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        var app = builder.Build();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        return app;
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MiniPainterHub.Server.Tests.Data;

public sealed class EmailConfirmationMigrationTests
{
    private readonly ITestOutputHelper _output;

    public EmailConfirmationMigrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string PreviousMigration = "20260713194528_AddHobbyProjects";
    private const string EmailConfirmationMigration = "20260716192734_GrandfatherExistingEmailConfirmations";

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task GrandfatherExistingEmailConfirmations_ConfirmsExistingUsersAndDoesNotUndoConfirmation()
    {
        var connectionString = SqlServerTestDatabase.CreateConnectionString("EmailConfirmationMigration");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new AppDbContext(options);
        try
        {
            var migrator = context.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await SqlServerTestDatabase.VerifyAsync(context, _output);
            context.Users.Add(new ApplicationUser
            {
                Id = "legacy-user",
                UserName = "legacy-user",
                NormalizedUserName = "LEGACY-USER",
                Email = "legacy@example.test",
                NormalizedEmail = "LEGACY@EXAMPLE.TEST",
                EmailConfirmed = false,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });
            await context.SaveChangesAsync();

            await migrator.MigrateAsync(EmailConfirmationMigration);
            context.ChangeTracker.Clear();
            (await context.Users.SingleAsync()).EmailConfirmed.Should().BeTrue();

            await migrator.MigrateAsync(PreviousMigration);
            context.ChangeTracker.Clear();
            (await context.Users.SingleAsync()).EmailConfirmed.Should().BeTrue("the one-way migration must not revoke a confirmed address");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}

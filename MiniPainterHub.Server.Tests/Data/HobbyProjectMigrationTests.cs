using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Data;

public sealed class HobbyProjectMigrationTests
{
    private const string PreviousMigration = "20260713025842_AddGoogleAuthentication";
    private const string HobbyProjectMigration = "20260713194528_AddHobbyProjects";

    [Fact]
    public async Task AddHobbyProjects_UpgradesWithoutBackfill_EnforcesRelationalIndexes_AndRoundTrips()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var databaseName = $"MiniPainterHub_HobbyProjectMigration_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new AppDbContext(options);
        try
        {
            var migrator = context.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            (await TableExistsAsync(context, "HobbyProjects")).Should().BeFalse();

            var owner = new ApplicationUser
            {
                Id = "migration-owner",
                UserName = "migration-owner",
                NormalizedUserName = "MIGRATION-OWNER",
                Email = "migration-owner@example.test",
                NormalizedEmail = "MIGRATION-OWNER@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            context.Users.Add(owner);
            var existingPost = NewPost(owner.Id, "Existing post", DateTime.UtcNow.AddDays(-1));
            context.Posts.Add(existingPost);
            await context.SaveChangesAsync();

            await migrator.MigrateAsync(HobbyProjectMigration);
            (await TableExistsAsync(context, "HobbyProjects")).Should().BeTrue();
            (await TableExistsAsync(context, "HobbyProjectEntries")).Should().BeTrue();
            (await context.Posts.CountAsync()).Should().Be(1);
            (await context.HobbyProjectEntries.CountAsync()).Should().Be(0, "the migration must not backfill existing posts");

            var secondPost = NewPost(owner.Id, "Post 2", DateTime.UtcNow);
            var thirdPost = NewPost(owner.Id, "Post 3", DateTime.UtcNow);
            context.Posts.AddRange(secondPost, thirdPost);
            var firstProject = NewProject(owner.Id, "First project");
            var secondProject = NewProject(owner.Id, "Second project");
            context.HobbyProjects.AddRange(firstProject, secondProject);
            await context.SaveChangesAsync();

            context.HobbyProjectEntries.Add(new HobbyProjectEntry
            {
                ProjectId = firstProject.Id,
                PostId = existingPost.Id,
                LinkedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            await using (var duplicateContext = new AppDbContext(options))
            {
                duplicateContext.HobbyProjectEntries.Add(new HobbyProjectEntry
                {
                    ProjectId = secondProject.Id,
                    PostId = existingPost.Id,
                    LinkedUtc = DateTime.UtcNow
                });
                await FluentActions.Invoking(async () => await duplicateContext.SaveChangesAsync())
                    .Should().ThrowAsync<DbUpdateException>("one post may belong to only one project");
            }

            context.HobbyProjectEntries.AddRange(
                new HobbyProjectEntry { ProjectId = firstProject.Id, PostId = secondPost.Id, LinkedUtc = DateTime.UtcNow },
                new HobbyProjectEntry { ProjectId = firstProject.Id, PostId = thirdPost.Id, LinkedUtc = DateTime.UtcNow });
            await context.SaveChangesAsync();
            var showcaseEntries = await context.HobbyProjectEntries
                .Where(entry => entry.ProjectId == firstProject.Id && entry.PostId != existingPost.Id)
                .OrderBy(entry => entry.PostId)
                .ToListAsync();
            showcaseEntries[0].ShowcaseOrder = 1;
            await context.SaveChangesAsync();
            showcaseEntries[1].ShowcaseOrder = 1;
            await FluentActions.Invoking(async () => await context.SaveChangesAsync())
                .Should().ThrowAsync<DbUpdateException>("non-null showcase order is unique within a project");
            context.ChangeTracker.Clear();

            await migrator.MigrateAsync(PreviousMigration);
            (await TableExistsAsync(context, "HobbyProjectEntries")).Should().BeFalse();
            (await TableExistsAsync(context, "HobbyProjects")).Should().BeFalse();
            (await context.Posts.CountAsync()).Should().Be(3, "downgrade must leave existing posts untouched");

            await migrator.MigrateAsync(HobbyProjectMigration);
            (await TableExistsAsync(context, "HobbyProjects")).Should().BeTrue();
            (await context.HobbyProjectEntries.CountAsync()).Should().Be(0);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static HobbyProject NewProject(string ownerUserId, string title) =>
        new()
        {
            OwnerUserId = ownerUserId,
            Title = title,
            Description = "Migration test project.",
            Kind = HobbyProjectKinds.Miniature,
            Status = HobbyProjectStatuses.Planning,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

    private static Post NewPost(string ownerUserId, string title, DateTime createdUtc) =>
        new()
        {
            CreatedById = ownerUserId,
            Title = title,
            Content = "Migration constraint check.",
            CreatedUtc = createdUtc,
            UpdatedUtc = createdUtc
        };

    private static async Task<bool> TableExistsAsync(AppDbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(@qualifiedName, N'U') IS NULL THEN 0 ELSE 1 END";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@qualifiedName";
        parameter.Value = $"[dbo].[{tableName}]";
        command.Parameters.Add(parameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }
}

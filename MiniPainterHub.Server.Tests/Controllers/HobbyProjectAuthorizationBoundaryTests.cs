using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public sealed class HobbyProjectAuthorizationBoundaryTests
{
    [Fact]
    public async Task OwnerMutation_WhenRequestedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        var seeded = await SeedProjectAsync(factory, "mutation-owner", includeLinkedPost: true);
        await SeedUserAsync(factory, "other-user");
        using var other = factory.CreateAuthenticatedClient("other-user", "other-user");

        var response = await other.PutAsJsonAsync($"/api/projects/{seeded.ProjectId}/status", new UpdateHobbyProjectStatusDto
        {
            Status = HobbyProjectStatuses.OnHold
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WhenDomainChoiceIsInvalid_ReturnsBadRequest()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "invalid-project-owner");
        using var client = factory.CreateAuthenticatedClient("invalid-project-owner", "invalid-project-owner");

        var response = await client.PostAsJsonAsync("/api/projects", new CreateHobbyProjectDto
        {
            Title = "Invalid project",
            Description = "Description",
            Kind = "NotAProjectKind"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CompleteWithoutImageShowcase_ReturnsConflict()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        var seeded = await SeedProjectAsync(factory, "completion-owner", includeLinkedPost: true, linkedPostHasImage: false);
        using var client = factory.CreateAuthenticatedClient("completion-owner", "completion-owner");

        var response = await client.PutAsJsonAsync($"/api/projects/{seeded.ProjectId}/status", new UpdateHobbyProjectStatusDto
        {
            Status = HobbyProjectStatuses.Completed
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SuspendedOwner_IsForbiddenFromProjectWrites_ButMayArchiveAndUnlink()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        var seeded = await SeedProjectAsync(
            factory,
            "suspended-project-owner",
            includeLinkedPost: true,
            linkedPostHasImage: true,
            suspendedUntilUtc: DateTime.UtcNow.AddHours(1));
        using var client = factory.CreateAuthenticatedClient("suspended-project-owner", "suspended-project-owner");

        var update = await client.PutAsJsonAsync($"/api/projects/{seeded.ProjectId}", new UpdateHobbyProjectDto
        {
            Title = "Blocked update",
            Description = "Description",
            Kind = HobbyProjectKinds.Army
        });
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var archive = await client.PostAsync($"/api/projects/{seeded.ProjectId}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);

        var unlink = await client.DeleteAsync($"/api/projects/{seeded.ProjectId}/posts/{seeded.LinkedPostId}");
        unlink.StatusCode.Should().Be(HttpStatusCode.OK);
        await factory.ExecuteDbContextAsync(async db =>
        {
            (await db.Posts.AnyAsync(post => post.Id == seeded.LinkedPostId && !post.IsDeleted)).Should().BeTrue();
            (await db.HobbyProjectEntries.AnyAsync(entry => entry.PostId == seeded.LinkedPostId)).Should().BeFalse();
        });
    }

    [Fact]
    public async Task NewPostsControl_BlocksPublishingAndLinking_ButAllowsCurationArchiveAndUnlink()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        var active = await SeedProjectAsync(factory, "paused-project-owner", includeLinkedPost: true, linkedPostHasImage: true);
        var archived = await SeedProjectAsync(factory, "paused-project-owner", includeLinkedPost: false, archived: true, createOwner: false);
        var availablePostId = await SeedPostAsync(factory, "paused-project-owner", hasImage: true);
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.AdminSiteControls.Add(new AdminSiteControl
            {
                Key = AdminSiteControlKeys.NewPosts,
                Enabled = false,
                Message = "Project publishing is paused.",
                Reason = "Test pause",
                UpdatedUtc = DateTime.UtcNow,
                UpdatedByUserId = "test-admin"
            });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateAuthenticatedClient("paused-project-owner", "paused-project-owner");

        (await client.PutAsJsonAsync($"/api/projects/{active.ProjectId}", new UpdateHobbyProjectDto
        {
            Title = "Metadata remains editable",
            Description = "The new-posts pause applies to publishing and linking, not project curation.",
            Kind = HobbyProjectKinds.Miniature
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/projects/{active.ProjectId}/status", new UpdateHobbyProjectStatusDto
        {
            Status = HobbyProjectStatuses.OnHold
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/projects/{active.ProjectId}/posts/{active.LinkedPostId}", new UpdateHobbyProjectEntryDto
        {
            MilestoneLabel = "Curation remains available"
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/projects/{active.ProjectId}/showcase", new UpdateHobbyProjectShowcaseDto
        {
            PostIds = new List<int> { active.LinkedPostId }
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/projects/{active.ProjectId}/cover", new UpdateHobbyProjectCoverDto
        {
            PostId = active.LinkedPostId
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsJsonAsync("/api/projects", NewProjectRequest("Blocked create"))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/api/projects/{archived.ProjectId}/restore", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync($"/api/projects/{active.ProjectId}/posts", new LinkHobbyProjectPostDto
        {
            PostId = availablePostId
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await client.PostAsync($"/api/projects/{active.ProjectId}/archive", null)).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/projects/{active.ProjectId}/posts/{active.LinkedPostId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    private static CreateHobbyProjectDto NewProjectRequest(string title) =>
        new()
        {
            Title = title,
            Description = "Project description",
            Kind = HobbyProjectKinds.Miniature
        };

    private static Task SeedUserAsync(IntegrationTestApplicationFactory factory, string userId) =>
        factory.ExecuteDbContextAsync(async db =>
        {
            if (await db.Users.AnyAsync(user => user.Id == userId))
            {
                return;
            }

            db.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@example.test",
                EmailConfirmed = true
            });
            await db.SaveChangesAsync();
        });

    private static async Task<int> SeedPostAsync(
        IntegrationTestApplicationFactory factory,
        string ownerUserId,
        bool hasImage)
    {
        var postId = 0;
        await factory.ExecuteDbContextAsync(async db =>
        {
            var owner = await db.Users.SingleAsync(user => user.Id == ownerUserId);
            var post = NewPost(owner, hasImage);
            db.Posts.Add(post);
            await db.SaveChangesAsync();
            postId = post.Id;
        });
        return postId;
    }

    private static async Task<SeededProject> SeedProjectAsync(
        IntegrationTestApplicationFactory factory,
        string ownerUserId,
        bool includeLinkedPost,
        bool linkedPostHasImage = false,
        DateTime? suspendedUntilUtc = null,
        bool archived = false,
        bool createOwner = true)
    {
        var result = new SeededProject();
        await factory.ExecuteDbContextAsync(async db =>
        {
            ApplicationUser owner;
            if (createOwner)
            {
                owner = new ApplicationUser
                {
                    Id = ownerUserId,
                    UserName = ownerUserId,
                    Email = $"{ownerUserId}@example.test",
                    EmailConfirmed = true,
                    SuspendedUntilUtc = suspendedUntilUtc
                };
                db.Users.Add(owner);
            }
            else
            {
                owner = await db.Users.SingleAsync(user => user.Id == ownerUserId);
            }

            var project = new HobbyProject
            {
                OwnerUserId = owner.Id,
                OwnerUser = owner,
                Title = $"{ownerUserId} project",
                Description = "Project description",
                Kind = HobbyProjectKinds.Army,
                Status = HobbyProjectStatuses.InProgress,
                CreatedUtc = DateTime.UtcNow.AddDays(-1),
                UpdatedUtc = DateTime.UtcNow,
                ArchivedUtc = archived ? DateTime.UtcNow : null
            };
            db.HobbyProjects.Add(project);
            if (includeLinkedPost)
            {
                var post = NewPost(owner, linkedPostHasImage);
                db.Posts.Add(post);
                project.Entries.Add(new HobbyProjectEntry
                {
                    Project = project,
                    Post = post,
                    LinkedUtc = DateTime.UtcNow
                });
                result.LinkedPostId = post.Id;
            }

            await db.SaveChangesAsync();
            result.ProjectId = project.Id;
            if (includeLinkedPost)
            {
                result.LinkedPostId = project.Entries.Single().PostId;
            }
        });
        return result;
    }

    private static Post NewPost(ApplicationUser owner, bool hasImage)
    {
        var post = new Post
        {
            CreatedById = owner.Id,
            CreatedBy = owner,
            Title = "Progress post",
            Content = "Progress update",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        if (hasImage)
        {
            post.Images.Add(new PostImage
            {
                ImageUrl = "/images/progress.jpg",
                ThumbnailUrl = "/images/progress-thumb.jpg"
            });
        }

        return post;
    }

    private sealed class SeededProject
    {
        public int ProjectId { get; set; }
        public int LinkedPostId { get; set; }
    }
}

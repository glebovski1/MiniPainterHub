using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public sealed class HobbyProjectBoundaryTests
{
    [Fact]
    public async Task CreateAsync_RejectsInvalidRequiredValues_AndFiftyFirstProject()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "project-cap-owner");
        var service = new HobbyProjectService(context);

        var invalid = await FluentActions.Invoking(() => service.CreateAsync(owner.Id, new CreateHobbyProjectDto
        {
            Title = "   ",
            Description = "Description",
            Kind = HobbyProjectKinds.Army
        })).Should().ThrowAsync<DomainValidationException>();
        invalid.Which.Errors.Should().ContainKey("title");

        context.HobbyProjects.AddRange(Enumerable.Range(1, HobbyProjectRules.MaxProjectsPerOwner).Select(index =>
            NewProject(owner, $"Project {index}", DateTime.UtcNow.AddMinutes(-index))));
        await context.SaveChangesAsync();

        await FluentActions.Invoking(() => service.CreateAsync(owner.Id, NewProjectRequest("One too many")))
            .Should().ThrowAsync<ConflictException>()
            .WithMessage("*at most 50*");
        (await context.HobbyProjects.CountAsync(project => project.OwnerUserId == owner.Id)).Should().Be(50);
    }

    [Fact]
    public async Task LinkPostAsync_RejectsTwoHundredFiftyFirstEntry_WithoutChangingMembership()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "entry-cap-owner");
        var project = NewProject(owner, "Full diary", DateTime.UtcNow);
        context.HobbyProjects.Add(project);

        var posts = Enumerable.Range(1, HobbyProjectRules.MaxEntriesPerProject + 1)
            .Select(index => NewPost(owner, index, DateTime.UtcNow.AddMinutes(-index), withImage: false))
            .ToList();
        context.Posts.AddRange(posts);
        foreach (var post in posts.Take(HobbyProjectRules.MaxEntriesPerProject))
        {
            project.Entries.Add(new HobbyProjectEntry
            {
                Project = project,
                Post = post,
                LinkedUtc = post.CreatedUtc
            });
        }

        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context);

        var unlinkedPostId = posts[^1].Id;
        await FluentActions.Invoking(() => service.LinkPostAsync(owner.Id, project.Id, new LinkHobbyProjectPostDto
        {
            PostId = unlinkedPostId
        })).Should().ThrowAsync<ConflictException>()
            .WithMessage("*at most 250*");

        (await context.HobbyProjectEntries.CountAsync(entry => entry.ProjectId == project.Id)).Should().Be(250);
        (await context.HobbyProjectEntries.AnyAsync(entry => entry.PostId == unlinkedPostId)).Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_AppliesCombinedFilters_AndPaginatesRecentOrderDeterministically()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "filter-owner");
        var otherOwner = await SeedUserAsync(context, "other-filter-owner");
        var baseline = DateTime.UtcNow.AddDays(-1);

        var matching = new[]
        {
            NewProject(owner, "Winter Alpha", baseline.AddMinutes(1), HobbyProjectKinds.Army, HobbyProjectStatuses.InProgress),
            NewProject(owner, "Winter Bravo", baseline.AddMinutes(2), HobbyProjectKinds.Army, HobbyProjectStatuses.InProgress),
            NewProject(owner, "Winter Charlie", baseline.AddMinutes(3), HobbyProjectKinds.Army, HobbyProjectStatuses.InProgress)
        };
        var wrongKind = NewProject(owner, "Winter Terrain", baseline.AddMinutes(4), HobbyProjectKinds.Terrain, HobbyProjectStatuses.InProgress);
        var wrongOwner = NewProject(otherOwner, "Winter Delta", baseline.AddMinutes(5), HobbyProjectKinds.Army, HobbyProjectStatuses.InProgress);
        var empty = NewProject(owner, "Winter Empty", baseline.AddMinutes(6), HobbyProjectKinds.Army, HobbyProjectStatuses.InProgress);
        context.HobbyProjects.AddRange(matching.Append(wrongKind).Append(wrongOwner).Append(empty));

        var id = 1;
        foreach (var project in matching.Append(wrongKind).Append(wrongOwner))
        {
            var post = NewPost(project.OwnerUser, id++, project.UpdatedUtc, withImage: false);
            context.Posts.Add(post);
            project.Entries.Add(new HobbyProjectEntry { Project = project, Post = post, LinkedUtc = post.CreatedUtc });
        }

        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context);
        var query = new HobbyProjectQueryDto
        {
            Search = "Winter",
            OwnerUserId = owner.Id,
            Kind = HobbyProjectKinds.Army,
            Status = HobbyProjectStatuses.InProgress,
            Sort = HobbyProjectSorts.Recent,
            PageNumber = 1,
            PageSize = 2
        };

        var first = await service.GetAllAsync(query);
        first.TotalCount.Should().Be(3);
        first.Items.Select(item => item.Title).Should().Equal("Winter Charlie", "Winter Bravo");

        query.PageNumber = 2;
        var second = await service.GetAllAsync(query);
        second.Items.Should().ContainSingle().Which.Title.Should().Be("Winter Alpha");
    }

    [Fact]
    public async Task UpdateEntryAsync_TrimsEdits_AndClearsMilestone()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "milestone-owner");
        var project = NewProject(owner, "Milestones", DateTime.UtcNow);
        var post = NewPost(owner, 1, DateTime.UtcNow, withImage: false);
        context.AddRange(project, post);
        project.Entries.Add(new HobbyProjectEntry
        {
            Project = project,
            Post = post,
            LinkedUtc = DateTime.UtcNow,
            MilestoneLabel = "Assembly"
        });
        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context);

        await service.UpdateEntryAsync(owner.Id, project.Id, post.Id, new UpdateHobbyProjectEntryDto
        {
            MilestoneLabel = "  First paint  "
        });
        (await service.GetDiaryAsync(project.Id, owner.Id, 1, 10)).Items.Single().MilestoneLabel.Should().Be("First paint");

        await service.UpdateEntryAsync(owner.Id, project.Id, post.Id, new UpdateHobbyProjectEntryDto
        {
            MilestoneLabel = "   "
        });
        (await service.GetDiaryAsync(project.Id, owner.Id, 1, 10)).Items.Single().MilestoneLabel.Should().BeNull();
    }

    [Fact]
    public async Task UpdateShowcaseAsync_EnforcesEligibilityAndCap_PreservesPriorOrderAfterFailure()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "showcase-owner");
        var project = NewProject(owner, "Showcase", DateTime.UtcNow);
        context.HobbyProjects.Add(project);
        var imagePosts = Enumerable.Range(1, HobbyProjectRules.MaxShowcaseEntries + 1)
            .Select(index => NewPost(owner, index, DateTime.UtcNow.AddMinutes(-index), withImage: true))
            .ToList();
        var textPost = NewPost(owner, 100, DateTime.UtcNow, withImage: false);
        context.Posts.AddRange(imagePosts.Append(textPost));
        foreach (var post in imagePosts.Append(textPost))
        {
            project.Entries.Add(new HobbyProjectEntry { Project = project, Post = post, LinkedUtc = post.CreatedUtc });
        }

        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context);
        await service.UpdateShowcaseAsync(owner.Id, project.Id, new UpdateHobbyProjectShowcaseDto
        {
            PostIds = new List<int> { imagePosts[1].Id, imagePosts[0].Id }
        });
        (await service.GetShowcaseAsync(project.Id, owner.Id, 1, 24)).Items.Select(item => item.PostId)
            .Should().Equal(imagePosts[1].Id, imagePosts[0].Id);

        await FluentActions.Invoking(() => service.UpdateShowcaseAsync(owner.Id, project.Id, new UpdateHobbyProjectShowcaseDto
        {
            PostIds = imagePosts.Select(post => post.Id).ToList()
        })).Should().ThrowAsync<DomainValidationException>();

        await FluentActions.Invoking(() => service.UpdateShowcaseAsync(owner.Id, project.Id, new UpdateHobbyProjectShowcaseDto
        {
            PostIds = new List<int> { imagePosts[0].Id, textPost.Id }
        })).Should().ThrowAsync<DomainValidationException>();

        (await service.GetShowcaseAsync(project.Id, owner.Id, 1, 24)).Items.Select(item => item.PostId)
            .Should().Equal(imagePosts[1].Id, imagePosts[0].Id);
    }

    [Fact]
    public async Task CoverResolution_UsesSelectedThenShowcaseThenNewestDiaryThenPlaceholder()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "cover-owner");
        var now = DateTime.UtcNow;
        var project = NewProject(owner, "Cover fallbacks", now);
        var oldest = NewPost(owner, 1, now.AddDays(-3), withImage: true);
        var selected = NewPost(owner, 2, now.AddDays(-2), withImage: true);
        var newest = NewPost(owner, 3, now.AddDays(-1), withImage: true);
        var textOnly = NewPost(owner, 4, now, withImage: false);
        context.Add(project);
        context.Posts.AddRange(oldest, selected, newest, textOnly);
        foreach (var post in new[] { oldest, selected, newest, textOnly })
        {
            project.Entries.Add(new HobbyProjectEntry { Project = project, Post = post, LinkedUtc = post.CreatedUtc });
        }

        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context);
        await service.UpdateShowcaseAsync(owner.Id, project.Id, new UpdateHobbyProjectShowcaseDto
        {
            PostIds = new List<int> { oldest.Id, newest.Id }
        });

        var explicitCover = await service.UpdateCoverAsync(owner.Id, project.Id, new UpdateHobbyProjectCoverDto { PostId = selected.Id });
        explicitCover.SelectedCoverPostId.Should().Be(selected.Id);
        explicitCover.CoverPostId.Should().Be(selected.Id);
        explicitCover.CoverImageUrl.Should().Be($"/images/{selected.Id}.jpg");

        var showcaseFallback = await service.UpdateCoverAsync(owner.Id, project.Id, new UpdateHobbyProjectCoverDto());
        showcaseFallback.SelectedCoverPostId.Should().BeNull();
        showcaseFallback.CoverPostId.Should().Be(oldest.Id);

        await service.UpdateShowcaseAsync(owner.Id, project.Id, new UpdateHobbyProjectShowcaseDto());
        var diaryFallback = await service.GetByIdAsync(project.Id, owner.Id);
        diaryFallback.CoverPostId.Should().Be(newest.Id);

        context.PostImages.RemoveRange(await context.PostImages.ToListAsync());
        await context.SaveChangesAsync();
        var placeholder = await service.GetByIdAsync(project.Id, owner.Id);
        placeholder.CoverPostId.Should().BeNull();
        placeholder.CoverImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmedMove_CleansSourceCuration_AndReopensCompletedDestination()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "move-owner");
        var source = NewProject(owner, "Source", DateTime.UtcNow.AddMinutes(-2));
        var destination = NewProject(owner, "Destination", DateTime.UtcNow.AddMinutes(-1));
        var movingPost = NewPost(owner, 1, DateTime.UtcNow.AddDays(-2), withImage: true);
        var destinationPost = NewPost(owner, 2, DateTime.UtcNow.AddDays(-1), withImage: true);
        context.AddRange(source, destination, movingPost, destinationPost);
        source.Entries.Add(new HobbyProjectEntry { Project = source, Post = movingPost, LinkedUtc = movingPost.CreatedUtc });
        destination.Entries.Add(new HobbyProjectEntry { Project = destination, Post = destinationPost, LinkedUtc = destinationPost.CreatedUtc });
        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context);
        await service.UpdateShowcaseAsync(owner.Id, source.Id, new UpdateHobbyProjectShowcaseDto { PostIds = new() { movingPost.Id } });
        await service.UpdateCoverAsync(owner.Id, source.Id, new UpdateHobbyProjectCoverDto { PostId = movingPost.Id });
        await service.UpdateShowcaseAsync(owner.Id, destination.Id, new UpdateHobbyProjectShowcaseDto { PostIds = new() { destinationPost.Id } });
        await service.UpdateStatusAsync(owner.Id, destination.Id, new UpdateHobbyProjectStatusDto { Status = HobbyProjectStatuses.Completed });

        var moved = await service.LinkPostAsync(owner.Id, destination.Id, new LinkHobbyProjectPostDto
        {
            PostId = movingPost.Id,
            SourceProjectId = source.Id,
            MilestoneLabel = "  Reinforcements  "
        });

        moved.Status.Should().Be(HobbyProjectStatuses.InProgress);
        moved.CompletedUtc.Should().BeNull();
        moved.EntryCount.Should().Be(2);
        moved.ShowcaseCount.Should().Be(1, "moving a post must clear its old showcase position");
        var sourceAfterMove = await service.GetByIdAsync(source.Id, owner.Id);
        sourceAfterMove.EntryCount.Should().Be(0);
        sourceAfterMove.ShowcaseCount.Should().Be(0);
        sourceAfterMove.SelectedCoverPostId.Should().BeNull();
        sourceAfterMove.CoverPostId.Should().BeNull();
        var diary = await service.GetDiaryAsync(destination.Id, owner.Id, 1, 10);
        diary.Items.Single(entry => entry.PostId == movingPost.Id).MilestoneLabel.Should().Be("Reinforcements");
        (await context.Posts.CountAsync()).Should().Be(2, "moving must never duplicate or delete posts");
    }

    [Fact]
    public async Task CompletedProject_RejectsBothWaysOfRemovingFinalShowcaseEntry()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "final-showcase-owner");
        var project = NewProject(owner, "Completed", DateTime.UtcNow);
        var post = NewPost(owner, 1, DateTime.UtcNow, withImage: true);
        context.AddRange(project, post);
        project.Entries.Add(new HobbyProjectEntry { Project = project, Post = post, LinkedUtc = post.CreatedUtc });
        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context);
        await service.UpdateShowcaseAsync(owner.Id, project.Id, new UpdateHobbyProjectShowcaseDto { PostIds = new() { post.Id } });
        await service.UpdateStatusAsync(owner.Id, project.Id, new UpdateHobbyProjectStatusDto { Status = HobbyProjectStatuses.Completed });

        await FluentActions.Invoking(() => service.UpdateShowcaseAsync(owner.Id, project.Id, new UpdateHobbyProjectShowcaseDto()))
            .Should().ThrowAsync<ConflictException>();
        await FluentActions.Invoking(() => service.UnlinkPostAsync(owner.Id, project.Id, post.Id))
            .Should().ThrowAsync<ConflictException>();

        var unchanged = await service.GetByIdAsync(project.Id, owner.Id);
        unchanged.Status.Should().Be(HobbyProjectStatuses.Completed);
        unchanged.CompletedUtc.Should().NotBeNull();
        unchanged.EntryCount.Should().Be(1);
        unchanged.ShowcaseCount.Should().Be(1);
    }

    [Fact]
    public async Task ArchivedProject_RequiresRestoreBeforeMetadataOrMilestoneEdits_ButAllowsUnlink()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "archived-edit-owner");
        var project = NewProject(owner, "Archived", DateTime.UtcNow);
        project.ArchivedUtc = DateTime.UtcNow;
        var post = NewPost(owner, 1, DateTime.UtcNow, withImage: true);
        context.AddRange(project, post);
        project.Entries.Add(new HobbyProjectEntry
        {
            Project = project,
            Post = post,
            LinkedUtc = post.CreatedUtc,
            MilestoneLabel = "Original"
        });
        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context);

        await FluentActions.Invoking(() => service.UpdateAsync(owner.Id, project.Id, new UpdateHobbyProjectDto
        {
            Title = "Blocked",
            Description = "Archived projects must be restored before editing.",
            Kind = HobbyProjectKinds.Army
        })).Should().ThrowAsync<ConflictException>();
        await FluentActions.Invoking(() => service.UpdateEntryAsync(owner.Id, project.Id, post.Id, new UpdateHobbyProjectEntryDto
        {
            MilestoneLabel = "Blocked"
        })).Should().ThrowAsync<ConflictException>();

        var unlinked = await service.UnlinkPostAsync(owner.Id, project.Id, post.Id);
        unlinked.EntryCount.Should().Be(0);
        unlinked.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task RestrictedAccount_CannotMutateOrRestore_ButCanArchiveAndUnlink()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "restricted-owner");
        var active = NewProject(owner, "Active", DateTime.UtcNow);
        var archived = NewProject(owner, "Archived", DateTime.UtcNow);
        archived.ArchivedUtc = DateTime.UtcNow;
        var post = NewPost(owner, 1, DateTime.UtcNow, withImage: true);
        context.AddRange(active, archived, post);
        active.Entries.Add(new HobbyProjectEntry { Project = active, Post = post, LinkedUtc = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context, new DenyingAccountRestrictionService());

        var restrictedActions = new Func<Task>[]
        {
            () => service.CreateAsync(owner.Id, NewProjectRequest("Blocked create")),
            () => service.UpdateAsync(owner.Id, active.Id, new UpdateHobbyProjectDto
            {
                Title = "Blocked update", Description = "Description", Kind = HobbyProjectKinds.Army
            }),
            () => service.UpdateStatusAsync(owner.Id, active.Id, new UpdateHobbyProjectStatusDto { Status = HobbyProjectStatuses.OnHold }),
            () => service.RestoreAsync(owner.Id, archived.Id),
            () => service.LinkPostAsync(owner.Id, active.Id, new LinkHobbyProjectPostDto { PostId = post.Id }),
            () => service.UpdateEntryAsync(owner.Id, active.Id, post.Id, new UpdateHobbyProjectEntryDto { MilestoneLabel = "Blocked" }),
            () => service.UpdateShowcaseAsync(owner.Id, active.Id, new UpdateHobbyProjectShowcaseDto { PostIds = new List<int> { post.Id } }),
            () => service.UpdateCoverAsync(owner.Id, active.Id, new UpdateHobbyProjectCoverDto { PostId = post.Id })
        };
        foreach (var action in restrictedActions)
        {
            await action.Should().ThrowAsync<ForbiddenException>().WithMessage("Restricted for test");
        }

        (await service.ArchiveAsync(owner.Id, active.Id)).IsArchived.Should().BeTrue();
        var unlinked = await service.UnlinkPostAsync(owner.Id, active.Id, post.Id);
        unlinked.EntryCount.Should().Be(0);
        (await context.Posts.AnyAsync(item => item.Id == post.Id && !item.IsDeleted)).Should().BeTrue();
    }

    [Fact]
    public async Task RestrictedAccount_CrossOwnerMutationsRemainNotFound()
    {
        await using var context = AppDbContextFactory.Create();
        var caller = await SeedUserAsync(context, "restricted-caller");
        var owner = await SeedUserAsync(context, "other-project-owner");
        var project = NewProject(owner, "Other painter project", DateTime.UtcNow);
        context.HobbyProjects.Add(project);
        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context, new DenyingAccountRestrictionService());

        var crossOwnerActions = new Func<Task>[]
        {
            () => service.UpdateAsync(caller.Id, project.Id, new UpdateHobbyProjectDto
            {
                Title = "Blocked update", Description = "Description", Kind = HobbyProjectKinds.Army
            }),
            () => service.UpdateStatusAsync(caller.Id, project.Id, new UpdateHobbyProjectStatusDto { Status = HobbyProjectStatuses.OnHold }),
            () => service.RestoreAsync(caller.Id, project.Id),
            () => service.LinkPostAsync(caller.Id, project.Id, new LinkHobbyProjectPostDto { PostId = 999 }),
            () => service.UpdateEntryAsync(caller.Id, project.Id, 999, new UpdateHobbyProjectEntryDto { MilestoneLabel = "Blocked" }),
            () => service.UpdateShowcaseAsync(caller.Id, project.Id, new UpdateHobbyProjectShowcaseDto()),
            () => service.UpdateCoverAsync(caller.Id, project.Id, new UpdateHobbyProjectCoverDto())
        };

        foreach (var action in crossOwnerActions)
        {
            await action.Should().ThrowAsync<NotFoundException>().WithMessage("Hobby project not found.");
        }
    }

    [Fact]
    public async Task RestrictedAccount_CanArchiveAndUnlinkFinalShowcaseWhileLifecycleReopensAtomically()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = await SeedUserAsync(context, "restricted-cleanup-owner");
        var project = NewProject(
            owner,
            "Completed cleanup",
            DateTime.UtcNow,
            status: HobbyProjectStatuses.Completed);
        project.CompletedUtc = DateTime.UtcNow.AddMinutes(-5);
        var post = NewPost(owner, 801, DateTime.UtcNow.AddDays(-1), withImage: true);
        context.AddRange(project, post);
        project.Entries.Add(new HobbyProjectEntry
        {
            Project = project,
            Post = post,
            LinkedUtc = post.CreatedUtc,
            ShowcaseOrder = 1
        });
        await context.SaveChangesAsync();
        var service = new HobbyProjectService(context, new DenyingAccountRestrictionService());

        var archived = await service.ArchiveAsync(owner.Id, project.Id);
        archived.IsArchived.Should().BeTrue();

        var cleaned = await service.UnlinkPostAsync(owner.Id, project.Id, post.Id);

        cleaned.IsArchived.Should().BeTrue();
        cleaned.Status.Should().Be(HobbyProjectStatuses.InProgress);
        cleaned.CompletedUtc.Should().BeNull();
        cleaned.EntryCount.Should().Be(0);
        (await context.Posts.AnyAsync(item => item.Id == post.Id && !item.IsDeleted)).Should().BeTrue();
        (await context.HobbyProjectEntries.AnyAsync(entry => entry.PostId == post.Id)).Should().BeFalse();
    }

    private static async Task<ApplicationUser> SeedUserAsync(AppDbContext context, string id)
    {
        var user = new ApplicationUser
        {
            Id = id,
            UserName = id,
            Email = $"{id}@example.test",
            EmailConfirmed = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static HobbyProject NewProject(
        ApplicationUser owner,
        string title,
        DateTime updatedUtc,
        string kind = HobbyProjectKinds.Miniature,
        string status = HobbyProjectStatuses.Planning) =>
        new()
        {
            OwnerUserId = owner.Id,
            OwnerUser = owner,
            Title = title,
            Description = "Project description",
            Kind = kind,
            Status = status,
            CreatedUtc = updatedUtc.AddDays(-1),
            UpdatedUtc = updatedUtc
        };

    private static CreateHobbyProjectDto NewProjectRequest(string title) =>
        new()
        {
            Title = title,
            Description = "Project description",
            Kind = HobbyProjectKinds.Miniature
        };

    private static Post NewPost(ApplicationUser owner, int id, DateTime createdUtc, bool withImage)
    {
        var post = new Post
        {
            Id = id,
            CreatedById = owner.Id,
            CreatedBy = owner,
            Title = $"Post {id}",
            Content = "Progress update",
            CreatedUtc = createdUtc,
            UpdatedUtc = createdUtc
        };
        if (withImage)
        {
            post.Images.Add(new PostImage
            {
                ImageUrl = $"/images/{id}.jpg",
                ThumbnailUrl = $"/images/{id}-thumb.jpg"
            });
        }

        return post;
    }

    private sealed class DenyingAccountRestrictionService : IAccountRestrictionService
    {
        public Task EnsureCanLoginAsync(ApplicationUser user) =>
            Task.FromException(new ForbiddenException("Restricted for test"));

        public Task EnsureCanRegisterAsync() => Task.CompletedTask;

        public Task EnsureCanCreatePostAsync(string userId) =>
            Task.FromException(new ForbiddenException("Restricted for test"));

        public Task EnsureCanCommentAsync(string userId) => Task.CompletedTask;
    }
}

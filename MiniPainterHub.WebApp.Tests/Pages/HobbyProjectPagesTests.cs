using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Projects;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public sealed class HobbyProjectPagesTests : TestContext
{
    private const string OwnerId = "owner-1";
    private const string DraftStorageKey = "minipainterhub.hobbyProjectDraft.v1";

    [Fact]
    public void ProjectList_CoversLoadingPopulatedFiltersAndPagination()
    {
        this.AddTestAuthorization().SetNotAuthorized();
        var firstPage = new TaskCompletionSource<ApiResult<PagedResult<HobbyProjectSummaryDto>?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queries = new List<HobbyProjectQueryDto>();
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetAllHandler = query =>
            {
                queries.Add(CloneQuery(query));
                return queries.Count == 1
                    ? firstPage.Task
                    : Task.FromResult(ProjectPage(new[] { Project() }, query.PageNumber, query.PageSize, totalCount: 25));
            }
        });

        var cut = RenderComponent<ProjectList>();
        cut.Find("[data-testid='projects-loading']").Should().NotBeNull();

        firstPage.SetResult(ProjectPage(new[] { Project() }, page: 1, pageSize: 12, totalCount: 25));
        cut.WaitForElement("[data-testid='project-list']");
        cut.Markup.Should().Contain("Winter army");
        cut.Markup.Should().Contain("Page 1 of 3");

        cut.Find("[data-testid='project-search']").Change("winter");
        cut.Find("[data-testid='project-kind-filter']").Change(HobbyProjectKinds.Army);
        cut.Find("[data-testid='project-status-filter']").Change(HobbyProjectStatuses.InProgress);
        cut.Find("[data-testid='project-sort']").Change(HobbyProjectSorts.Title);
        cut.Find("[data-testid='project-filter-apply']").Click();

        cut.WaitForAssertion(() =>
        {
            queries.Should().HaveCountGreaterThanOrEqualTo(2);
            queries[^1].Search.Should().Be("winter");
            queries[^1].Kind.Should().Be(HobbyProjectKinds.Army);
            queries[^1].Status.Should().Be(HobbyProjectStatuses.InProgress);
            queries[^1].Sort.Should().Be(HobbyProjectSorts.Title);
            queries[^1].PageNumber.Should().Be(1);
        });

        cut.Find("nav[aria-label='Project result pages'] button:last-of-type").Click();
        cut.WaitForAssertion(() => queries[^1].PageNumber.Should().Be(2));
    }

    [Theory]
    [InlineData(false, "projects-empty")]
    [InlineData(true, "projects-error")]
    public void ProjectList_CoversEmptyAndErrorStates(bool fail, string expectedTestId)
    {
        this.AddTestAuthorization().SetNotAuthorized();
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetAllHandler = query => Task.FromResult(fail
                ? new ApiResult<PagedResult<HobbyProjectSummaryDto>?>(false, HttpStatusCode.ServiceUnavailable, null)
                : ProjectPage(Array.Empty<HobbyProjectSummaryDto>(), query.PageNumber, query.PageSize))
        });

        var cut = RenderComponent<ProjectList>();

        cut.WaitForElement($"[data-testid='{expectedTestId}']");
    }

    [Fact]
    public void MyProjects_CoversLoadingPopulatedFiltersAndPagination()
    {
        this.SetAuthenticatedUser(OwnerId, "owner");
        var firstPage = new TaskCompletionSource<ApiResult<PagedResult<HobbyProjectSummaryDto>?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queries = new List<HobbyProjectQueryDto>();
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetMineHandler = query =>
            {
                queries.Add(CloneQuery(query));
                return queries.Count == 1
                    ? firstPage.Task
                    : Task.FromResult(ProjectPage(new[] { Project() }, query.PageNumber, query.PageSize, totalCount: 25));
            }
        });

        var cut = RenderComponent<MyProjects>();
        cut.Find("[data-testid='my-projects-loading']").Should().NotBeNull();

        firstPage.SetResult(ProjectPage(new[] { Project() }, page: 1, pageSize: 12, totalCount: 25));
        cut.WaitForElement("[data-testid='my-project-list']");
        cut.Find("[data-testid='hobby-project-manage-link']").GetAttribute("href").Should().Be("/projects/7/edit");
        cut.Markup.Should().Contain("Page 1 of 3");

        cut.Find("[data-testid='my-project-search']").Change("force");
        cut.Find("[data-testid='my-project-kind']").Change(HobbyProjectKinds.Army);
        cut.Find("[data-testid='my-project-status']").Change(HobbyProjectStatuses.Completed);
        cut.Find("[data-testid='my-project-archived']").Change(true);
        cut.Find("[data-testid='my-project-filter-apply']").Click();

        cut.WaitForAssertion(() =>
        {
            queries.Should().HaveCountGreaterThanOrEqualTo(2);
            queries[^1].Search.Should().Be("force");
            queries[^1].Kind.Should().Be(HobbyProjectKinds.Army);
            queries[^1].Status.Should().Be(HobbyProjectStatuses.Completed);
            queries[^1].IncludeArchived.Should().BeTrue();
            queries[^1].PageNumber.Should().Be(1);
        });

        cut.Find("nav[aria-label='Your project pages'] button:last-of-type").Click();
        cut.WaitForAssertion(() => queries[^1].PageNumber.Should().Be(2));
    }

    [Theory]
    [InlineData(false, "my-projects-empty")]
    [InlineData(true, "my-projects-error")]
    public void MyProjects_CoversEmptyAndErrorStates(bool fail, string expectedTestId)
    {
        this.SetAuthenticatedUser(OwnerId, "owner");
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetMineHandler = query => Task.FromResult(fail
                ? new ApiResult<PagedResult<HobbyProjectSummaryDto>?>(false, HttpStatusCode.ServiceUnavailable, null)
                : ProjectPage(Array.Empty<HobbyProjectSummaryDto>(), query.PageNumber, query.PageSize))
        });

        var cut = RenderComponent<MyProjects>();

        cut.WaitForElement($"[data-testid='{expectedTestId}']");
    }

    [Fact]
    public async Task CreateProject_RestoresDraftAndPreservesItAfterFailedSubmission()
    {
        this.SetAuthenticatedUser(OwnerId, "owner");
        var js = new RecordingJsRuntime();
        js.LocalStorage[DraftStorageKey] = JsonSerializer.Serialize(new CreateHobbyProjectDto
        {
            Title = "Restored winter force",
            Description = "A project draft that must survive a recoverable API failure.",
            Kind = HobbyProjectKinds.Army
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Services.AddSingleton<IJSRuntime>(js);
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            CreateHandler = _ => Task.FromResult(new ApiResult<HobbyProjectDto?>(false, HttpStatusCode.ServiceUnavailable, null))
        });

        var cut = RenderComponent<CreateProject>();

        cut.WaitForElement("[data-testid='project-create-draft-restored']");
        cut.Find("[data-testid='project-create-title']").GetAttribute("value").Should().Be("Restored winter force");

        await cut.Find("[data-testid='project-create-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='project-create-error']").TextContent.Should().Contain("draft is still here");
            cut.Find("[data-testid='project-create-title']").GetAttribute("value").Should().Be("Restored winter force");
            js.LocalStorage.Should().ContainKey(DraftStorageKey);
        });
    }

    [Fact]
    public void ProjectDetails_UsesQueryBackedAccessibleTabsAndDiaryAnchors()
    {
        this.AddTestAuthorization().SetNotAuthorized();
        var project = Project(status: HobbyProjectStatuses.Completed);
        var diaryEntry = Entry(11, "Assembly", imageUrl: "/images/assembly.jpg");
        var showcaseEntry = Entry(12, "Finished", imageUrl: "/images/finished.jpg", showcaseOrder: 0);
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetHandler = _ => ProjectResult(project),
            GetDiaryHandler = (_, page, pageSize) => Task.FromResult(EntryPage(new[] { diaryEntry }, page, pageSize)),
            GetShowcaseHandler = (_, page, pageSize) => Task.FromResult(EntryPage(new[] { showcaseEntry }, page, pageSize))
        });
        this.AddModerationStub();
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project.Id}?view=diary");

        var cut = RenderComponent<ProjectDetails>(parameters => parameters.Add(component => component.Id, project.Id));

        cut.WaitForElement("#post-11");
        cut.Find("#project-diary-tab").GetAttribute("aria-selected").Should().Be("true");
        cut.Find("#project-diary-panel").GetAttribute("aria-labelledby").Should().Be("project-diary-tab");

        cut.Find("#project-showcase-tab").Click();

        cut.WaitForAssertion(() =>
        {
            Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith($"/projects/{project.Id}?view=showcase");
            cut.Find("#project-showcase-tab").GetAttribute("aria-selected").Should().Be("true");
            cut.Find("#project-showcase-panel").GetAttribute("aria-labelledby").Should().Be("project-showcase-tab");
            cut.Markup.Should().Contain("Finished");
        });
    }

    [Fact]
    public void ProjectDetails_ShowcaseFallsBackToOriginalWhenThumbnailFails()
    {
        this.AddTestAuthorization().SetNotAuthorized();
        var project = Project(status: HobbyProjectStatuses.Completed);
        project.ShowcaseCount = 1;
        var entry = Entry(13, "Finished standard", imageUrl: "/images/finished-full.jpg", showcaseOrder: 0);
        entry.Post.ThumbnailUrl = "/api/images/thumbnail?url=finished-full.jpg";
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetHandler = _ => ProjectResult(project),
            GetShowcaseHandler = (_, page, pageSize) => Task.FromResult(EntryPage(new[] { entry }, page, pageSize))
        });
        this.AddModerationStub();
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project.Id}?view=showcase");

        var cut = RenderComponent<ProjectDetails>(parameters => parameters.Add(component => component.Id, project.Id));
        var image = cut.WaitForElement("[data-testid='project-showcase'] img");
        image.GetAttribute("src").Should().Be(entry.Post.ThumbnailUrl);

        image.TriggerEvent("onerror", EventArgs.Empty);

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='project-showcase'] img").GetAttribute("src").Should().Be(entry.Post.ImageUrl));
    }

    [Theory]
    [InlineData(true, false, "project-hidden-state")]
    [InlineData(false, true, "project-archived-state")]
    public void ProjectDetails_HiddenOrArchivedOwnerCannotStartProgress(bool hidden, bool archived, string stateTestId)
    {
        this.SetAuthenticatedUser(OwnerId, "owner");
        var project = Project(hidden: hidden, archived: archived);
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetHandler = _ => ProjectResult(project)
        });
        this.AddModerationStub();

        var cut = RenderComponent<ProjectDetails>(parameters => parameters.Add(component => component.Id, project.Id));

        cut.WaitForElement($"[data-testid='{stateTestId}']");
        cut.FindAll("[data-testid='project-add-update-link']").Should().BeEmpty();
        cut.FindAll($"a[href='/posts/new?projectId={project.Id}']").Should().BeEmpty();
        cut.Find("[data-testid='project-edit-link']").Should().NotBeNull();
    }

    [Fact]
    public void EditProject_RequiresExplicitMoveConfirmationAndSendsSourceProjectId()
    {
        this.SetAuthenticatedUser(OwnerId, "owner");
        var project = Project();
        var source = new HobbyProjectReferenceDto { Id = 19, Title = "Old warband", IsPublic = true };
        var availablePost = Post(71, "Shared captain", "/images/captain.jpg", source);
        LinkHobbyProjectPostDto? captured = null;
        var linked = false;
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetHandler = _ => ProjectResult(project),
            GetAvailablePostsHandler = (_, _, page, pageSize) => Task.FromResult(PostPage(linked ? Array.Empty<PostSummaryDto>() : new[] { availablePost }, page, pageSize)),
            LinkPostHandler = (_, request) =>
            {
                captured = request;
                linked = true;
                return ProjectResult(project);
            }
        });

        var cut = RenderComponent<EditProject>(parameters => parameters.Add(component => component.Id, project.Id));
        cut.WaitForElement("[data-testid='project-link-post']").Click();

        captured.Should().BeNull("the source project must not be changed before confirmation");
        cut.Find("[data-testid='project-move-confirmation']").TextContent.Should().Contain("Old warband");
        cut.Find("[data-testid='project-move-confirm']").Click();

        cut.WaitForAssertion(() =>
        {
            captured.Should().NotBeNull();
            captured!.PostId.Should().Be(availablePost.Id);
            captured.SourceProjectId.Should().Be(source.Id);
            cut.Find("[data-testid='project-edit-announcement']").TextContent.Should().Contain("moved from Old warband");
        });
    }

    [Fact]
    public void EditProject_LinkedPostFallsBackToOriginalWhenThumbnailFails()
    {
        this.SetAuthenticatedUser(OwnerId, "owner");
        var project = Project();
        var entry = Entry(72, "Weathered captain", imageUrl: "/images/captain-full.jpg");
        entry.Post.ThumbnailUrl = "/api/images/thumbnail?url=captain-full.jpg";
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetHandler = _ => ProjectResult(project),
            GetDiaryHandler = (_, page, pageSize) => Task.FromResult(EntryPage(new[] { entry }, page, pageSize))
        });

        var cut = RenderComponent<EditProject>(parameters => parameters.Add(component => component.Id, project.Id));
        var image = cut.WaitForElement("[data-testid='project-entry-row'] img");
        image.GetAttribute("src").Should().Be(entry.Post.ThumbnailUrl);

        image.TriggerEvent("onerror", EventArgs.Empty);

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='project-entry-row'] img").GetAttribute("src").Should().Be(entry.Post.ImageUrl));
    }

    [Fact]
    public void EditProject_BlocksCompletionUntilImageBackedShowcaseIsSaved()
    {
        this.SetAuthenticatedUser(OwnerId, "owner");
        var project = Project();
        var imageEntry = Entry(31, "Finished squad", imageUrl: "/images/squad.jpg");
        var showcaseSaved = false;
        UpdateHobbyProjectStatusDto? statusRequest = null;
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetHandler = _ => ProjectResult(project),
            GetDiaryHandler = (_, page, pageSize) => Task.FromResult(EntryPage(new[] { imageEntry }, page, pageSize)),
            GetShowcaseHandler = (_, page, pageSize) => Task.FromResult(EntryPage(showcaseSaved ? new[] { imageEntry } : Array.Empty<HobbyProjectEntryDto>(), page, pageSize)),
            UpdateShowcaseHandler = (_, request) =>
            {
                request.PostIds.Should().Equal(imageEntry.PostId);
                showcaseSaved = true;
                project.ShowcaseCount = 1;
                return ProjectResult(project);
            },
            UpdateStatusHandler = (_, request) =>
            {
                statusRequest = request;
                project.Status = request.Status;
                return ProjectResult(project);
            }
        });

        var cut = RenderComponent<EditProject>(parameters => parameters.Add(component => component.Id, project.Id));
        cut.WaitForElement("[data-testid='project-status-select']").Change(HobbyProjectStatuses.Completed);

        cut.Find("[data-testid='project-completion-requirement']").TextContent.Should().Contain("Select and save");
        cut.Find("[data-testid='project-status-save']").HasAttribute("disabled").Should().BeTrue();

        cut.Find("[data-testid='project-entry-row'] input[type='checkbox']").Change(true);
        cut.Find("[data-testid='showcase-save']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='project-completion-requirement']").TextContent.Should().Contain("Ready to complete");
            cut.Find("[data-testid='project-status-save']").HasAttribute("disabled").Should().BeFalse();
            cut.Find("[data-testid='project-edit-announcement']").TextContent.Should().Contain("Showcase selection and order saved");
        });

        cut.Find("[data-testid='project-status-save']").Click();
        cut.WaitForAssertion(() =>
        {
            statusRequest!.Status.Should().Be(HobbyProjectStatuses.Completed);
            cut.Find("[data-testid='project-edit-announcement']").TextContent.Should().Contain("Status changed to Completed");
        });
    }

    [Fact]
    public void EditProject_HiddenStateBlocksAdditionsAndExplainsImageEligibility()
    {
        this.SetAuthenticatedUser(OwnerId, "owner");
        var project = Project(hidden: true);
        var textOnlyEntry = Entry(41, "Priming notes");
        var availablePost = Post(42, "Next update", "/images/update.jpg");
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetHandler = _ => ProjectResult(project),
            GetDiaryHandler = (_, page, pageSize) => Task.FromResult(EntryPage(new[] { textOnlyEntry }, page, pageSize)),
            GetAvailablePostsHandler = (_, _, page, pageSize) => Task.FromResult(PostPage(new[] { availablePost }, page, pageSize))
        });

        var cut = RenderComponent<EditProject>(parameters => parameters.Add(component => component.Id, project.Id));

        cut.WaitForElement("[data-testid='project-additions-hidden']");
        cut.FindAll($"a[href='/posts/new?projectId={project.Id}']").Should().BeEmpty();
        cut.Find("[data-testid='project-link-post']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='project-image-required']").TextContent.Should().Contain("Add an image");
        cut.FindAll("[data-testid='project-entry-row'] input[type='checkbox']").Should().BeEmpty();
        cut.FindAll("[data-testid='project-cover-set']").Should().BeEmpty();
    }

    [Fact]
    public void HobbyProjectCard_ShowsHiddenAndArchivedIndependently()
    {
        var project = Project(hidden: true, archived: true);

        var cut = RenderComponent<HobbyProjectCard>(parameters => parameters
            .Add(component => component.Project, project)
            .Add(component => component.ShowOwner, false)
            .Add(component => component.ShowManage, true));

        cut.Find("[data-testid='hobby-project-hidden-badge']").TextContent.Should().Contain("Hidden by staff");
        cut.Markup.Should().Contain("Archived");
        cut.Markup.Should().NotContain("Owner-only setup");
    }

    private static HobbyProjectDto Project(
        string status = HobbyProjectStatuses.Planning,
        bool hidden = false,
        bool archived = false) => new()
        {
            Id = 7,
            OwnerUserId = OwnerId,
            OwnerUserName = "owner",
            OwnerDisplayName = "Project Owner",
            Title = "Winter army",
            Description = "A weathered force built one unit at a time.",
            Kind = HobbyProjectKinds.Army,
            Status = status,
            IsHidden = hidden,
            IsArchived = archived,
            IsPublic = !hidden && !archived,
            CreatedUtc = DateTime.UtcNow.AddDays(-10),
            UpdatedUtc = DateTime.UtcNow
        };

    private static HobbyProjectEntryDto Entry(int postId, string title, string? imageUrl = null, int? showcaseOrder = null) => new()
    {
        Id = postId + 100,
        ProjectId = 7,
        PostId = postId,
        LinkedUtc = DateTime.UtcNow,
        ShowcaseOrder = showcaseOrder,
        Post = Post(postId, title, imageUrl)
    };

    private static PostSummaryDto Post(int id, string title, string? imageUrl = null, HobbyProjectReferenceDto? project = null) => new()
    {
        Id = id,
        Title = title,
        Snippet = $"Notes for {title}",
        AuthorId = OwnerId,
        AuthorName = "owner",
        ImageUrl = imageUrl,
        CreatedAt = DateTime.UtcNow,
        Project = project
    };

    private static Task<ApiResult<HobbyProjectDto?>> ProjectResult(HobbyProjectDto project) =>
        Task.FromResult(new ApiResult<HobbyProjectDto?>(true, HttpStatusCode.OK, project));

    private static ApiResult<PagedResult<HobbyProjectEntryDto>?> EntryPage(IEnumerable<HobbyProjectEntryDto> entries, int page, int pageSize)
    {
        var items = entries.ToArray();
        return new ApiResult<PagedResult<HobbyProjectEntryDto>?>(true, HttpStatusCode.OK, new PagedResult<HobbyProjectEntryDto>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = items.Length
        });
    }

    private static ApiResult<PagedResult<PostSummaryDto>?> PostPage(IEnumerable<PostSummaryDto> posts, int page, int pageSize)
    {
        var items = posts.ToArray();
        return new ApiResult<PagedResult<PostSummaryDto>?>(true, HttpStatusCode.OK, new PagedResult<PostSummaryDto>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = items.Length
        });
    }

    private static ApiResult<PagedResult<HobbyProjectSummaryDto>?> ProjectPage(
        IEnumerable<HobbyProjectSummaryDto> projects,
        int page,
        int pageSize,
        int? totalCount = null)
    {
        var items = projects.ToArray();
        return new ApiResult<PagedResult<HobbyProjectSummaryDto>?>(true, HttpStatusCode.OK, new PagedResult<HobbyProjectSummaryDto>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = totalCount ?? items.Length
        });
    }

    private static HobbyProjectQueryDto CloneQuery(HobbyProjectQueryDto query) => new()
    {
        Search = query.Search,
        OwnerUserId = query.OwnerUserId,
        Kind = query.Kind,
        Status = query.Status,
        Sort = query.Sort,
        IncludeArchived = query.IncludeArchived,
        PageNumber = query.PageNumber,
        PageSize = query.PageSize
    };
}

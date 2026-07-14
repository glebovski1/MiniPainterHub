using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Posts;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Posts;

public class CreateTests : TestContext
{
    private const string DraftStorageKey = "minipainterhub.createPostDraft.v1";

    [Fact]
    public void RendersCreatePostFormWithStableSelectors()
    {
        AddComposerStubs();

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-form']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-title']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-content']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-recipe-fields']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-miniature']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-paints-used']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-techniques']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-upload-zone']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-readiness']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-submit']").Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_WithoutImages_CreatesPostNavigatesAndClearsDraft()
    {
        CreatePostDto? captured = null;
        var js = AddComposerStubs(new StubPostService
        {
            CreateHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(new PostDto
                {
                    Id = 123,
                    Title = dto.Title,
                    Content = dto.Content,
                    CreatedById = "user-1"
                });
            }
        });

        var cut = RenderComponent<Create>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("[data-testid='create-post-title']").Change("New title");
        cut.Find("[data-testid='create-post-content']").Change("New content");
        js.LocalStorage.Should().ContainKey(DraftStorageKey);

        await cut.Find("[data-testid='create-post-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            nav.Uri.Should().Be("http://localhost/posts/123");
            captured.Should().NotBeNull();
            captured!.Title.Should().Be("New title");
            captured.Content.Should().Be("New content");
            js.LocalStorage.Should().NotContainKey(DraftStorageKey);
        });
    }

    [Fact]
    public async Task Submit_WithTags_PreviewsDistinctTagsAndSendsThemToTheApi()
    {
        CreatePostDto? captured = null;
        AddComposerStubs(new StubPostService
        {
            CreateHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(new PostDto
                {
                    Id = 124,
                    Title = dto.Title,
                    Content = dto.Content,
                    CreatedById = "user-1"
                });
            }
        });

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-title']").Change("Tagged title");
        cut.Find("[data-testid='create-post-content']").Change("Tagged content");
        cut.Find("[data-testid='create-post-tags']").Input("glazing, NMM, glazing");

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='create-post-tag-chip']").Should().HaveCount(2));

        await cut.Find("[data-testid='create-post-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            captured.Should().NotBeNull();
            captured!.Tags.Should().Equal("glazing", "NMM");
        });
    }

    [Fact]
    public async Task Submit_WithRecipeFields_SendsThemToTheApiAndShowsPreview()
    {
        CreatePostDto? captured = null;
        AddComposerStubs(new StubPostService
        {
            CreateHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(new PostDto
                {
                    Id = 126,
                    Title = dto.Title,
                    Content = dto.Content,
                    CreatedById = "user-1",
                    MiniatureName = dto.MiniatureName,
                    PaintsUsed = dto.PaintsUsed,
                    Techniques = dto.Techniques,
                    Difficulty = dto.Difficulty,
                    TimeSpent = dto.TimeSpent
                });
            }
        });

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-title']").Change("Recipe title");
        cut.Find("[data-testid='create-post-content']").Change("Recipe content");
        cut.Find("[data-testid='create-post-miniature']").Change("Stormcast Liberator");
        cut.Find("[data-testid='create-post-paints-used']").Change("Retributor Armour, Reikland Fleshshade");
        cut.Find("[data-testid='create-post-techniques']").Change("Layering and glazing");
        cut.Find("[data-testid='create-post-difficulty']").Change("Intermediate");
        cut.Find("[data-testid='create-post-time-spent']").Change("4 hours");

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='create-post-recipe-preview']").TextContent.Should().Contain("Stormcast Liberator"));

        await cut.Find("[data-testid='create-post-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            captured.Should().NotBeNull();
            captured!.MiniatureName.Should().Be("Stormcast Liberator");
            captured.PaintsUsed.Should().Be("Retributor Armour, Reikland Fleshshade");
            captured.Techniques.Should().Be("Layering and glazing");
            captured.Difficulty.Should().Be("Intermediate");
            captured.TimeSpent.Should().Be("4 hours");
        });
    }

    [Fact]
    public void Draft_RestoresTextAndTagsAndCanBeDiscarded()
    {
        var js = AddComposerStubs();
        js.LocalStorage[DraftStorageKey] = JsonSerializer.Serialize(new
        {
            title = "Draft title",
            content = "Draft body",
            miniatureName = "Test miniature",
            tags = "glazing, nmm"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var cut = RenderComponent<Create>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='create-post-draft-restored']").TextContent.Should().Contain("Draft restored");
            cut.Find("[data-testid='create-post-title']").GetAttribute("value").Should().Be("Draft title");
            cut.Find("[data-testid='create-post-tags']").GetAttribute("value").Should().Be("glazing, nmm");
        });

        cut.Find("[data-testid='create-post-discard-draft']").Click();

        cut.WaitForAssertion(() =>
        {
            js.LocalStorage.Should().NotContainKey(DraftStorageKey);
            cut.Find("[data-testid='create-post-title']").GetAttribute("value").Should().BeNullOrEmpty();
            cut.Find("[data-testid='create-post-tags']").GetAttribute("value").Should().BeNullOrEmpty();
        });
    }

    [Fact]
    public void Draft_SavesTextAndTagsButDoesNotPersistFiles()
    {
        var js = AddComposerStubs();
        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-title']").Change("Autosaved title");
        cut.Find("[data-testid='create-post-tags']").Input("weathering");
        cut.FindComponent<InputFile>().UploadFiles(CreateImage("cover.png", "image/png"));

        cut.WaitForAssertion(() =>
        {
            js.LocalStorage.Should().ContainKey(DraftStorageKey);
            var json = js.LocalStorage[DraftStorageKey];
            json.Should().Contain("Autosaved title");
            json.Should().Contain("weathering");
            json.Should().NotContain("cover.png");
        });
    }

    [Fact]
    public void Tags_ShowSuggestionsAndInsertSelectedSuggestion()
    {
        AddComposerStubs(searchStub: new StubSearchService
        {
            SearchTagsHandler = (query, page, pageSize) =>
                Task.FromResult(new ApiResult<PagedResult<SearchTagResultDto>?>(true, HttpStatusCode.OK, new PagedResult<SearchTagResultDto>
                {
                    Items = new[]
                    {
                        new SearchTagResultDto { Name = "glazing", Slug = "glazing", PostCount = 12 },
                        new SearchTagResultDto { Name = "glow-effects", Slug = "glow-effects", PostCount = 4 }
                    },
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalCount = 2
                }))
        });

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-tags']").Input("gl");

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='create-post-tag-suggestion']").Should().HaveCount(2));

        cut.FindAll("[data-testid='create-post-tag-suggestion']")[0].Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='create-post-tags']").GetAttribute("value").Should().Be("glazing");
            cut.Find("[data-testid='create-post-tags-preview']").TextContent.Should().Contain("#glazing");
        });
    }

    [Fact]
    public async Task Tags_WhenSuggestionSearchFails_StillAllowsManualTagSubmit()
    {
        CreatePostDto? captured = null;
        AddComposerStubs(
            postStub: new StubPostService
            {
                CreateHandler = dto =>
                {
                    captured = dto;
                    return Task.FromResult(new PostDto { Id = 127, Title = dto.Title, Content = dto.Content, CreatedById = "user-1" });
                }
            },
            searchStub: new StubSearchService
            {
                SearchTagsHandler = (_, _, _) =>
                    Task.FromResult(new ApiResult<PagedResult<SearchTagResultDto>?>(false, HttpStatusCode.InternalServerError, null))
            });

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-title']").Change("Manual tag title");
        cut.Find("[data-testid='create-post-content']").Change("Manual tag content");
        cut.Find("[data-testid='create-post-tags']").Input("zz");

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='create-post-tag-suggestion-message']").TextContent.Should().Contain("Suggestions are unavailable"));

        await cut.Find("[data-testid='create-post-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            captured.Should().NotBeNull();
            captured!.Tags.Should().Equal("zz");
        });
    }

    [Fact]
    public void UploadValidation_BlocksUnsupportedOversizedAndExtraImages()
    {
        AddComposerStubs();
        var cut = RenderComponent<Create>();
        var files = Enumerable.Range(1, PostImageUploadRules.MaxImagesPerPost - 2)
            .Select(index => CreateImage($"valid-{index}.png", "image/png"))
            .Append(InputFileContent.CreateFromBinary(new byte[1], "bad.gif", null, "image/gif"))
            .Append(InputFileContent.CreateFromBinary(new byte[PostImageUploadRules.MaxUploadBytes + 1], "large.jpg", null, "image/jpeg"))
            .Append(CreateImage("extra.png", "image/png"))
            .ToArray();

        cut.FindComponent<InputFile>().UploadFiles(files);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='create-post-image-card']").Should().HaveCount(PostImageUploadRules.MaxImagesPerPost + 1);
            cut.Find("[data-testid='create-post-upload-warning']").TextContent.Should().Contain("Check the image queue");
            cut.Find("[data-testid='create-post-submit']").HasAttribute("disabled").Should().BeTrue();
            cut.Markup.Should().Contain("Remove this extra image");
            cut.Markup.Should().Contain("Use JPEG, PNG, or WEBP images");
            cut.Markup.Should().Contain("at or below 20 MB");
        });
    }

    [Fact]
    public void UploadValidation_RemovingInvalidImageAllowsSubmit()
    {
        AddComposerStubs();
        var cut = RenderComponent<Create>();

        cut.FindComponent<InputFile>().UploadFiles(
            CreateImage("valid.png", "image/png"),
            InputFileContent.CreateFromBinary(new byte[1], "bad.gif", null, "image/gif"));

        cut.Find("[data-testid='create-post-submit']").HasAttribute("disabled").Should().BeTrue();

        cut.FindAll("[data-testid='create-post-image-remove']")[1].Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='create-post-image-card']").Should().ContainSingle();
            cut.Find("[data-testid='create-post-submit']").HasAttribute("disabled").Should().BeFalse();
        });
    }

    [Fact]
    public async Task Submit_WithImages_UsesDisplayedOrderAndClearsDraft()
    {
        var imageNames = new List<string>();
        var tagValues = new List<string>();
        var js = AddComposerStubs(new StubPostService
        {
            CreateWithImageHandler = async content =>
            {
                foreach (var part in content)
                {
                    var disposition = part.Headers.ContentDisposition;
                    var name = disposition?.Name?.Trim('"');
                    if (name == "images")
                    {
                        imageNames.Add(disposition?.FileName?.Trim('"') ?? string.Empty);
                    }
                    else if (name == "tags")
                    {
                        tagValues.Add(await part.ReadAsStringAsync());
                    }
                }

                return new PostDto { Id = 128, Title = "Uploaded", Content = "Uploaded content", CreatedById = "user-1" };
            }
        });

        var cut = RenderComponent<Create>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("[data-testid='create-post-title']").Change("Image title");
        cut.Find("[data-testid='create-post-content']").Change("Image content");
        cut.Find("[data-testid='create-post-tags']").Input("glazing");
        cut.FindComponent<InputFile>().UploadFiles(
            CreateImage("first.png", "image/png"),
            CreateImage("second.webp", "image/webp"));

        cut.FindAll("[data-testid='create-post-image-move-down']")[0].Click();
        await cut.Find("[data-testid='create-post-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            nav.Uri.Should().Be("http://localhost/posts/128");
            imageNames.Should().Equal("second.webp", "first.png");
            tagValues.Should().Equal("glazing");
            js.LocalStorage.Should().NotContainKey(DraftStorageKey);
        });
    }

    [Fact]
    public async Task Submit_WhenServiceThrows_ShowsErrorMessageAndKeepsDraft()
    {
        var js = AddComposerStubs(new StubPostService
        {
            CreateHandler = _ => Task.FromException<PostDto>(new InvalidOperationException("Create failed"))
        });

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-title']").Change("New title");
        cut.Find("[data-testid='create-post-content']").Change("New content");
        await cut.Find("[data-testid='create-post-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='create-post-error']").TextContent.Should().Contain("Create failed");
            js.LocalStorage.Should().ContainKey(DraftStorageKey);
        });
    }

    [Fact]
    public async Task Submit_WhileRequestIsInFlight_DisablesFormAndPreventsDuplicateCreateCalls()
    {
        var submitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubmit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var createCalls = 0;

        AddComposerStubs(new StubPostService
        {
            CreateHandler = async dto =>
            {
                createCalls++;
                submitStarted.TrySetResult();
                await releaseSubmit.Task;
                return new PostDto
                {
                    Id = 125,
                    Title = dto.Title,
                    Content = dto.Content,
                    CreatedById = "user-1"
                };
            }
        });

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-title']").Change("New title");
        cut.Find("[data-testid='create-post-content']").Change("New content");

        var firstSubmit = cut.Find("[data-testid='create-post-form']").SubmitAsync();
        await submitStarted.Task;

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='create-post-submit']").HasAttribute("disabled").Should().BeTrue();
            cut.Find("[data-testid='create-post-submit']").TextContent.Should().Contain("Publishing...");
            cut.Find("[data-testid='create-post-cancel']").HasAttribute("disabled").Should().BeTrue();
        });

        await cut.Find("[data-testid='create-post-form']").SubmitAsync();
        createCalls.Should().Be(1);

        releaseSubmit.TrySetResult();
        await firstSubmit;
    }

    [Fact]
    public void CancelButton_NavigatesToHome()
    {
        AddComposerStubs();
        var cut = RenderComponent<Create>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("[data-testid='create-post-cancel']").Click();

        nav.Uri.Should().Be("http://localhost/");
    }

    [Fact]
    public async Task RequestedProject_WhenUnavailable_BlocksPublishUntilExplicitStandaloneChoice()
    {
        CreatePostDto? captured = null;
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetMineHandler = query => Task.FromResult(ProjectPage(new[] { ProjectSummary(7, "Available project") }, query))
        });
        AddComposerStubs(new StubPostService
        {
            CreateHandler = request =>
            {
                captured = request;
                return Task.FromResult(new PostDto { Id = 201, Title = request.Title, Content = request.Content, CreatedById = "user-1" });
            }
        });
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/posts/new?projectId=999");

        var cut = RenderComponent<Create>();
        cut.WaitForElement("[data-testid='create-post-without-project']");
        cut.Find("[data-testid='create-post-project-message']").TextContent.Should().Contain("requested project is unavailable");
        cut.Find("[data-testid='create-post-project']").GetAttribute("value").Should().BeNullOrEmpty();
        cut.Find("[data-testid='create-post-submit']").HasAttribute("disabled").Should().BeTrue();

        cut.Find("[data-testid='create-post-title']").Change("Standalone update");
        cut.Find("[data-testid='create-post-content']").Change("Published only after an explicit standalone choice.");
        cut.Find("[data-testid='create-post-without-project']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='create-post-without-project']").Should().BeEmpty();
            cut.Find("[data-testid='create-post-submit']").HasAttribute("disabled").Should().BeFalse();
        });

        await cut.Find("[data-testid='create-post-form']").SubmitAsync();
        cut.WaitForAssertion(() =>
        {
            captured.Should().NotBeNull();
            captured!.ProjectId.Should().BeNull();
            captured.MilestoneLabel.Should().BeNull();
            nav.Uri.Should().Be("http://localhost/posts/201");
        });
    }

    [Fact]
    public async Task RequestedProject_PersistsProjectAndMilestoneAndReturnsToDiaryAnchor()
    {
        CreatePostDto? captured = null;
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetMineHandler = query => Task.FromResult(ProjectPage(new[] { ProjectSummary(7, "Winter army") }, query))
        });
        var js = AddComposerStubs(new StubPostService
        {
            CreateHandler = request =>
            {
                captured = request;
                return Task.FromResult(new PostDto { Id = 202, Title = request.Title, Content = request.Content, CreatedById = "user-1" });
            }
        });
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/posts/new?projectId=7");

        var cut = RenderComponent<Create>();
        cut.WaitForElement("[data-testid='create-post-milestone']");
        cut.Find("[data-testid='create-post-project']").GetAttribute("value").Should().Be("7");
        cut.Find("[data-testid='create-post-project-message']").TextContent.Should().Contain("Winter army");

        cut.Find("[data-testid='create-post-title']").Change("First squad complete");
        cut.Find("[data-testid='create-post-content']").Change("The first finished unit for the winter force.");
        cut.Find("[data-testid='create-post-milestone']").Change("First squad finished");

        cut.WaitForAssertion(() =>
        {
            js.LocalStorage[DraftStorageKey].Should().Contain("\"projectId\":7");
            js.LocalStorage[DraftStorageKey].Should().Contain("\"milestoneLabel\":\"First squad finished\"");
        });

        await cut.Find("[data-testid='create-post-form']").SubmitAsync();
        cut.WaitForAssertion(() =>
        {
            captured.Should().NotBeNull();
            captured!.ProjectId.Should().Be(7);
            captured.MilestoneLabel.Should().Be("First squad finished");
            nav.Uri.Should().Be("http://localhost/projects/7?view=diary#post-202");
            js.LocalStorage.Should().NotContainKey(DraftStorageKey);
        });
    }

    private static ApiResult<PagedResult<HobbyProjectSummaryDto>?> ProjectPage(
        IReadOnlyList<HobbyProjectSummaryDto> projects,
        HobbyProjectQueryDto query) =>
        new(true, HttpStatusCode.OK, new PagedResult<HobbyProjectSummaryDto>
        {
            Items = projects,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = projects.Count
        });

    private static HobbyProjectSummaryDto ProjectSummary(int id, string title) => new()
    {
        Id = id,
        OwnerUserId = "user-1",
        OwnerUserName = "painter",
        OwnerDisplayName = "Painter",
        Title = title,
        Description = "A project available to the post composer.",
        Kind = HobbyProjectKinds.Army,
        Status = HobbyProjectStatuses.InProgress,
        IsPublic = true,
        UpdatedUtc = DateTime.UtcNow
    };

    private RecordingJsRuntime AddComposerStubs(StubPostService? postStub = null, StubSearchService? searchStub = null)
    {
        var js = new RecordingJsRuntime();
        Services.AddSingleton<IJSRuntime>(js);
        this.AddPostStub(postStub);
        this.AddSearchStub(searchStub);
        return js;
    }

    private static InputFileContent CreateImage(string fileName, string contentType) =>
        InputFileContent.CreateFromBinary(new byte[] { 1, 2, 3, 4 }, fileName, null, contentType);
}

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Features.Posts;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public sealed class HobbyProjectServiceTests
{
    [Fact]
    public async Task CreateAsync_TrimsValues_AndKeepsEmptyProjectOwnerOnly()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        var service = new HobbyProjectService(context);

        var created = await service.CreateAsync("owner", new CreateHobbyProjectDto
        {
            Title = "  Frost army  ",
            Description = "  A winter force.  ",
            Kind = "army",
            GameSystem = "  Northlands  "
        });

        created.Title.Should().Be("Frost army");
        created.Description.Should().Be("A winter force.");
        created.Kind.Should().Be(HobbyProjectKinds.Army);
        created.GameSystem.Should().Be("Northlands");
        created.Status.Should().Be(HobbyProjectStatuses.Planning);
        created.IsPublic.Should().BeFalse();
        await FluentActions.Invoking(() => service.GetByIdAsync(created.Id))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task LinkPostAsync_MakesProjectPublic_AndReturnsMilestoneInDiary()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        await SeedPostAsync(context, 10, "owner", withImage: true);
        var service = new HobbyProjectService(context);
        var project = await CreateProjectAsync(service, "owner");

        await service.LinkPostAsync("owner", project.Id, new LinkHobbyProjectPostDto
        {
            PostId = 10,
            MilestoneLabel = "  Base colors complete  "
        });

        var publicProject = await service.GetByIdAsync(project.Id);
        publicProject.IsPublic.Should().BeTrue();
        publicProject.EntryCount.Should().Be(1);
        var diary = await service.GetDiaryAsync(project.Id, null, 1, 10);
        diary.Items.Should().ContainSingle().Which.MilestoneLabel.Should().Be("Base colors complete");
    }

    [Fact]
    public async Task CompletionRequiresShowcase_AndNewEntryReopensProject()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        await SeedPostAsync(context, 10, "owner", withImage: true);
        await SeedPostAsync(context, 11, "owner", withImage: false);
        var logger = new RecordingLogger<HobbyProjectService>();
        var service = new HobbyProjectService(context, logger: logger);
        var project = await CreateProjectAsync(service, "owner");
        await service.LinkPostAsync("owner", project.Id, new LinkHobbyProjectPostDto { PostId = 10 });

        await FluentActions.Invoking(() => service.UpdateStatusAsync("owner", project.Id, new UpdateHobbyProjectStatusDto
        {
            Status = HobbyProjectStatuses.Completed
        })).Should().ThrowAsync<ConflictException>();

        await service.UpdateShowcaseAsync("owner", project.Id, new UpdateHobbyProjectShowcaseDto { PostIds = new List<int> { 10 } });
        var completed = await service.UpdateStatusAsync("owner", project.Id, new UpdateHobbyProjectStatusDto { Status = HobbyProjectStatuses.Completed });
        completed.CompletedUtc.Should().NotBeNull();

        logger.Messages.Clear();
        var reopened = await service.LinkPostAsync("owner", project.Id, new LinkHobbyProjectPostDto { PostId = 11 });
        reopened.Status.Should().Be(HobbyProjectStatuses.InProgress);
        reopened.CompletedUtc.Should().BeNull();
        logger.Messages.Should().ContainSingle(message => message.Contains("Action=Reopened", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShowcaseUpdate_LeavesUnselectedDiaryEntriesOutsideShowcase()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        await SeedPostAsync(context, 10, "owner", withImage: true);
        await SeedPostAsync(context, 11, "owner", withImage: true);
        var service = new HobbyProjectService(context);
        var project = await CreateProjectAsync(service, "owner");
        await service.LinkPostAsync("owner", project.Id, new LinkHobbyProjectPostDto { PostId = 10 });
        await service.LinkPostAsync("owner", project.Id, new LinkHobbyProjectPostDto { PostId = 11 });

        await service.UpdateShowcaseAsync("owner", project.Id, new UpdateHobbyProjectShowcaseDto { PostIds = new List<int> { 11 } });

        context.ChangeTracker.Clear();
        (await context.HobbyProjectEntries.SingleAsync(entry => entry.PostId == 11)).ShowcaseOrder.Should().Be(1);
        (await context.HobbyProjectEntries.SingleAsync(entry => entry.PostId == 10)).ShowcaseOrder.Should().BeNull();
    }

    [Fact]
    public async Task MoveFinalVisibleShowcasePostOutOfCompletedProject_IsRejected()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        await SeedPostAsync(context, 10, "owner", withImage: true);
        var service = new HobbyProjectService(context);
        var source = await CreateProjectAsync(service, "owner", "Source");
        var target = await CreateProjectAsync(service, "owner", "Target");
        await service.LinkPostAsync("owner", source.Id, new LinkHobbyProjectPostDto { PostId = 10 });
        await service.UpdateShowcaseAsync("owner", source.Id, new UpdateHobbyProjectShowcaseDto { PostIds = new List<int> { 10 } });
        await service.UpdateStatusAsync("owner", source.Id, new UpdateHobbyProjectStatusDto { Status = HobbyProjectStatuses.Completed });

        await FluentActions.Invoking(() => service.LinkPostAsync("owner", target.Id, new LinkHobbyProjectPostDto
        {
            PostId = 10,
            SourceProjectId = source.Id
        })).Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task LinkAlreadyLinkedPost_ReturnsStructuredCurrentProjectReference()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        await SeedPostAsync(context, 10, "owner", withImage: false);
        var service = new HobbyProjectService(context);
        var source = await CreateProjectAsync(service, "owner", "Source");
        var target = await CreateProjectAsync(service, "owner", "Target");
        await service.LinkPostAsync("owner", source.Id, new LinkHobbyProjectPostDto { PostId = 10 });

        var sameProject = await FluentActions.Invoking(() => service.LinkPostAsync("owner", source.Id, new LinkHobbyProjectPostDto { PostId = 10 }))
            .Should().ThrowAsync<HobbyProjectLinkConflictException>();
        sameProject.Which.CurrentProject.Id.Should().Be(source.Id);
        sameProject.Which.CurrentProject.Title.Should().Be("Source");

        var otherProject = await FluentActions.Invoking(() => service.LinkPostAsync("owner", target.Id, new LinkHobbyProjectPostDto { PostId = 10 }))
            .Should().ThrowAsync<HobbyProjectLinkConflictException>();
        otherProject.Which.CurrentProject.Id.Should().Be(source.Id);
        otherProject.Which.CurrentProject.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task LinkPostAsync_WhenRelationalRaceLoses_ReturnsWinningProjectReferenceAndReusableContext()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var databaseName = $"MiniPainterHub_HobbyProjectLinkRace_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        var baseOptions = new DbContextOptionsBuilder<MiniPainterHub.Server.Data.AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var setup = new MiniPainterHub.Server.Data.AppDbContext(baseOptions);
        try
        {
            await setup.Database.MigrateAsync();
            var owner = new ApplicationUser
            {
                Id = "race-owner",
                UserName = "race-owner",
                NormalizedUserName = "RACE-OWNER",
                Email = "race-owner@example.test",
                NormalizedEmail = "RACE-OWNER@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            var winner = new HobbyProject
            {
                OwnerUser = owner,
                OwnerUserId = owner.Id,
                Title = "Winning project",
                Description = "Won the concurrent link.",
                Kind = HobbyProjectKinds.Miniature,
                Status = HobbyProjectStatuses.Planning,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            var attempted = new HobbyProject
            {
                OwnerUser = owner,
                OwnerUserId = owner.Id,
                Title = "Attempted project",
                Description = "Lost the concurrent link.",
                Kind = HobbyProjectKinds.Miniature,
                Status = HobbyProjectStatuses.Planning,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            var post = new Post
            {
                CreatedBy = owner,
                CreatedById = owner.Id,
                Title = "Contended post",
                Content = "Two requests attempt the same membership.",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            setup.AddRange(owner, winner, attempted, post);
            await setup.SaveChangesAsync();

            var interceptor = new WinningProjectLinkInterceptor(baseOptions, winner.Id, post.Id);
            var losingOptions = new DbContextOptionsBuilder<MiniPainterHub.Server.Data.AppDbContext>()
                .UseSqlServer(connectionString)
                .AddInterceptors(interceptor)
                .Options;
            await using var losingContext = new MiniPainterHub.Server.Data.AppDbContext(losingOptions);
            var service = new HobbyProjectService(losingContext);

            var conflict = await FluentActions.Invoking(() => service.LinkPostAsync(
                    owner.Id,
                    attempted.Id,
                    new LinkHobbyProjectPostDto { PostId = post.Id }))
                .Should().ThrowAsync<HobbyProjectLinkConflictException>();

            conflict.Which.CurrentProject.Id.Should().Be(winner.Id);
            conflict.Which.CurrentProject.Title.Should().Be("Winning project");
            (await service.GetByIdAsync(winner.Id, owner.Id)).Id.Should().Be(winner.Id,
                "the failed tracker must be cleared before recovering the winning membership");
            (await losingContext.HobbyProjectEntries.CountAsync(entry => entry.PostId == post.Id)).Should().Be(1);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task GetAvailablePostsAsync_ExcludesCurrentProjectEntries_ButKeepsMoveCandidates()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        await SeedPostAsync(context, 10, "owner", withImage: true);
        await SeedPostAsync(context, 11, "owner", withImage: true);
        await SeedPostAsync(context, 12, "owner", withImage: false);
        var service = new HobbyProjectService(context);
        var current = await CreateProjectAsync(service, "owner", "Current");
        var other = await CreateProjectAsync(service, "owner", "Other");
        await service.LinkPostAsync("owner", current.Id, new LinkHobbyProjectPostDto { PostId = 10 });
        await service.LinkPostAsync("owner", other.Id, new LinkHobbyProjectPostDto { PostId = 11 });

        var available = await service.GetAvailablePostsAsync("owner", current.Id, null, 1, 10);

        available.Items.Should().HaveCount(2);
        available.Items.Should().NotContain(post => post.Id == 10);
        available.Items.Should().Contain(post => post.Id == 12 && post.Project == null);
        var moveCandidate = available.Items.Should().ContainSingle(post => post.Id == 11).Which;
        moveCandidate.Project.Should().NotBeNull();
        moveCandidate.Project!.Id.Should().Be(other.Id);
        moveCandidate.Project.Title.Should().Be("Other");
    }

    [Fact]
    public async Task HiddenPost_IsExcludedFromOwnerProjection_AndProducesCurationWarning()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        await SeedPostAsync(context, 10, "owner", withImage: true);
        var service = new HobbyProjectService(context);
        var project = await CreateProjectAsync(service, "owner");
        await service.LinkPostAsync("owner", project.Id, new LinkHobbyProjectPostDto { PostId = 10 });
        await service.UpdateShowcaseAsync("owner", project.Id, new UpdateHobbyProjectShowcaseDto { PostIds = new List<int> { 10 } });
        await service.UpdateStatusAsync("owner", project.Id, new UpdateHobbyProjectStatusDto { Status = HobbyProjectStatuses.Completed });
        var post = await context.Posts.FindAsync(10);
        post!.IsDeleted = true;
        await context.SaveChangesAsync();

        var details = await service.GetByIdAsync(project.Id, "owner");
        details.EntryCount.Should().Be(0);
        details.ShowcaseCount.Should().Be(0);
        details.CoverImageUrl.Should().BeNull();
        details.HasCurationWarning.Should().BeTrue();
        var diary = await service.GetDiaryAsync(project.Id, "owner", 1, 10);
        diary.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ArchiveHidesPublicProject_ButOwnerCanRestoreIt()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        await SeedPostAsync(context, 10, "owner", withImage: false);
        var service = new HobbyProjectService(context);
        var project = await CreateProjectAsync(service, "owner");
        await service.LinkPostAsync("owner", project.Id, new LinkHobbyProjectPostDto { PostId = 10 });

        var archived = await service.ArchiveAsync("owner", project.Id);
        archived.IsArchived.Should().BeTrue();
        await FluentActions.Invoking(() => service.GetByIdAsync(project.Id)).Should().ThrowAsync<NotFoundException>();

        var restored = await service.RestoreAsync("owner", project.Id);
        restored.IsArchived.Should().BeFalse();
        (await service.GetByIdAsync(project.Id)).IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task PostServiceCreateAsync_PersistsPostAndProjectEntryInOneSave()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        var projects = new HobbyProjectService(context);
        var project = await CreateProjectAsync(projects, "owner");
        var postService = CreatePostService(context, projects);

        var post = await postService.CreateAsync("owner", new CreatePostDto
        {
            Title = "First update",
            Content = "Started the armor.",
            ProjectId = project.Id,
            MilestoneLabel = "Assembly"
        });

        post.Project.Should().NotBeNull();
        post.Project!.Id.Should().Be(project.Id);
        context.HobbyProjectEntries.Should().ContainSingle(entry => entry.PostId == post.Id && entry.MilestoneLabel == "Assembly");
    }

    [Fact]
    public async Task LinkNewPostAsync_LogsSuccessOnlyAfterDatabaseCommit()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        var logger = new RecordingLogger<HobbyProjectService>();
        var projects = new HobbyProjectService(context, logger: logger);
        var project = await CreateProjectAsync(projects, "owner", "Sensitive project title");
        logger.Messages.Clear();
        var post = new Post
        {
            CreatedById = "owner",
            Title = "Sensitive post title",
            Content = "Progress update",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        context.Posts.Add(post);

        await projects.LinkNewPostAsync("owner", post, project.Id, "First checkpoint");

        logger.Messages.Should().NotContain(message => message.Contains("Action=NewPostLinked", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("Action=FirstEntryLinked", StringComparison.Ordinal));

        await context.SaveChangesAsync();

        logger.Messages.Should().ContainSingle(message => message.Contains("Action=NewPostLinked", StringComparison.Ordinal));
        logger.Messages.Should().ContainSingle(message => message.Contains("Action=FirstEntryLinked", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("Sensitive", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedProjectAwareImageUpload_RemovesPostAndEntry()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        var projects = new HobbyProjectService(context);
        var project = await CreateProjectAsync(projects, "owner");
        var postService = CreatePostService(context, projects, new ThrowingImageProcessor());
        var file = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "images", "update.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        await FluentActions.Invoking(() => postService.CreateWithImagesAsync("owner", new CreateImagePostDto
        {
            Title = "Failed update",
            Content = "This upload fails.",
            ProjectId = project.Id,
            Images = new List<IFormFile> { file }
        }, CancellationToken.None)).Should().ThrowAsync<InvalidOperationException>();

        context.ChangeTracker.Clear();
        context.Posts.Should().BeEmpty();
        context.HobbyProjectEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedImageUpload_RestoresCompletedProjectLifecycleExactly()
    {
        await using var context = AppDbContextFactory.Create();
        await SeedUserAsync(context, "owner");
        await SeedPostAsync(context, 10, "owner", withImage: true);
        var projects = new HobbyProjectService(context);
        var project = await CreateProjectAsync(projects, "owner");
        await projects.LinkPostAsync("owner", project.Id, new LinkHobbyProjectPostDto { PostId = 10 });
        await projects.UpdateShowcaseAsync("owner", project.Id, new UpdateHobbyProjectShowcaseDto { PostIds = new List<int> { 10 } });
        var completed = await projects.UpdateStatusAsync("owner", project.Id, new UpdateHobbyProjectStatusDto { Status = HobbyProjectStatuses.Completed });
        var postService = CreatePostService(context, projects, new ThrowingImageProcessor());
        var file = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "images", "update.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        await FluentActions.Invoking(() => postService.CreateWithImagesAsync("owner", new CreateImagePostDto
        {
            Title = "Failed update",
            Content = "This upload fails.",
            ProjectId = project.Id,
            Images = new List<IFormFile> { file }
        }, CancellationToken.None)).Should().ThrowAsync<InvalidOperationException>();

        context.ChangeTracker.Clear();
        var restored = await context.HobbyProjects.SingleAsync(item => item.Id == project.Id);
        restored.Status.Should().Be(HobbyProjectStatuses.Completed);
        restored.CompletedUtc.Should().Be(completed.CompletedUtc);
        restored.UpdatedUtc.Should().Be(completed.UpdatedUtc);
        context.Posts.Should().ContainSingle(post => post.Id == 10);
        context.HobbyProjectEntries.Should().ContainSingle(entry => entry.PostId == 10);
    }

    private static async Task<HobbyProjectDto> CreateProjectAsync(HobbyProjectService service, string userId, string title = "Project") =>
        await service.CreateAsync(userId, new CreateHobbyProjectDto
        {
            Title = title,
            Description = "Project description",
            Kind = HobbyProjectKinds.Miniature
        });

    private static async Task SeedUserAsync(MiniPainterHub.Server.Data.AppDbContext context, string userId)
    {
        context.Users.Add(new ApplicationUser { Id = userId, UserName = userId, Email = $"{userId}@example.test" });
        await context.SaveChangesAsync();
    }

    private static async Task SeedPostAsync(MiniPainterHub.Server.Data.AppDbContext context, int id, string userId, bool withImage)
    {
        var post = new Post
        {
            Id = id,
            CreatedById = userId,
            Title = $"Post {id}",
            Content = "Progress update",
            CreatedUtc = DateTime.UtcNow.AddMinutes(-id),
            UpdatedUtc = DateTime.UtcNow.AddMinutes(-id)
        };
        if (withImage)
        {
            post.Images.Add(new PostImage { ImageUrl = $"/images/{id}.jpg", ThumbnailUrl = $"/images/{id}-thumb.jpg" });
        }

        context.Posts.Add(post);
        await context.SaveChangesAsync();
    }

    private static PostService CreatePostService(
        MiniPainterHub.Server.Data.AppDbContext context,
        HobbyProjectService projects,
        IImageProcessor? processor = null)
    {
        var attachments = new PostImageAttachmentService(
            context,
            new StubImageService(),
            processor ?? new StubImageProcessor(),
            new StubImageStore(),
            Microsoft.Extensions.Options.Options.Create(new ImagesOptions { Enabled = true }),
            NullLogger<PostImageAttachmentService>.Instance,
            projects);
        return new PostService(context, attachments, hobbyProjectPostLinker: projects);
    }

    private sealed class StubImageService : IImageService
    {
        public Task DeleteAsync(string fileName) => Task.CompletedTask;
        public Task<Stream> DownloadAsync(string fileName) => Task.FromResult<Stream>(Stream.Null);
        public Task<string> UploadAsync(Stream fileStream, string fileName) => Task.FromResult("/" + fileName);
    }

    private sealed class StubImageProcessor : IImageProcessor
    {
        public Task<ImageVariants> ProcessAsync(Stream stream, string? contentType, CancellationToken ct)
        {
            var image = new ImageVariant(new byte[] { 1 }, "image/jpeg", "jpg", 1, 1);
            return Task.FromResult(new ImageVariants(image, image, image));
        }
    }

    private sealed class ThrowingImageProcessor : IImageProcessor
    {
        public Task<ImageVariants> ProcessAsync(Stream stream, string? contentType, CancellationToken ct) =>
            throw new InvalidOperationException("Image processing failed.");
    }

    private sealed class StubImageStore : IImageStore
    {
        public Task<ImageStoreResult> SaveAsync(Guid postId, Guid imageId, ImageVariants variants, CancellationToken ct) =>
            Task.FromResult(new ImageStoreResult("/max.jpg", "/preview.jpg", "/thumb.jpg"));

        public Task DeleteAsync(Guid postId, Guid imageId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NoopScope : IDisposable
        {
            public static NoopScope Instance { get; } = new();
            public void Dispose()
            {
            }
        }
    }

    private sealed class WinningProjectLinkInterceptor : SaveChangesInterceptor
    {
        private readonly DbContextOptions<MiniPainterHub.Server.Data.AppDbContext> _options;
        private readonly int _winningProjectId;
        private readonly int _postId;
        private int _triggered;

        public WinningProjectLinkInterceptor(
            DbContextOptions<MiniPainterHub.Server.Data.AppDbContext> options,
            int winningProjectId,
            int postId)
        {
            _options = options;
            _winningProjectId = winningProjectId;
            _postId = postId;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _triggered, 1) != 0)
            {
                return result;
            }

            await using var winningContext = new MiniPainterHub.Server.Data.AppDbContext(_options);
            winningContext.HobbyProjectEntries.Add(new HobbyProjectEntry
            {
                ProjectId = _winningProjectId,
                PostId = _postId,
                LinkedUtc = DateTime.UtcNow
            });
            await winningContext.SaveChangesAsync(cancellationToken);
            return result;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Features.Pagination;
using MiniPainterHub.Server.Features.Posts;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public sealed class HobbyProjectService : IHobbyProjectService, IHobbyProjectPostLinker
{
    private readonly AppDbContext _dbContext;
    private readonly IAccountRestrictionService? _accountRestrictionService;
    private readonly ILogger<HobbyProjectService> _logger;
    private readonly Dictionary<Post, ProjectRollbackState> _pendingPostRollbacks = new();

    public HobbyProjectService(
        AppDbContext dbContext,
        IAccountRestrictionService? accountRestrictionService = null,
        ILogger<HobbyProjectService>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _accountRestrictionService = accountRestrictionService;
        _logger = logger ?? NullLogger<HobbyProjectService>.Instance;
        _dbContext.SavedChanges += (_, _) => LogCommittedNewPostLinks();
    }

    public async Task<PagedResult<HobbyProjectSummaryDto>> GetAllAsync(HobbyProjectQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var filter = ValidateQuery(query);
        IQueryable<HobbyProject> projects = _dbContext.HobbyProjects
            .AsNoTracking()
            .Where(project =>
                !project.IsHidden
                && project.ArchivedUtc == null
                && project.Entries.Any(entry => !entry.Post.IsDeleted));

        projects = ApplyFilters(projects, filter);
        return await GetProjectPageAsync(projects, filter, ownerView: false);
    }

    public async Task<PagedResult<HobbyProjectSummaryDto>> GetMineAsync(string userId, HobbyProjectQueryDto query)
    {
        EnsureUserId(userId);
        ArgumentNullException.ThrowIfNull(query);
        var filter = ValidateQuery(query);
        var projects = ApplyFilters(
            _dbContext.HobbyProjects.AsNoTracking().Where(project => project.OwnerUserId == userId),
            filter,
            includeArchiveFilter: true);
        return await GetProjectPageAsync(projects, filter, ownerView: true);
    }

    public async Task<HobbyProjectDto> GetByIdAsync(int projectId, string? currentUserId = null)
    {
        var project = await ProjectGraph(asTracking: false)
            .FirstOrDefaultAsync(item => item.Id == projectId)
            ?? throw new NotFoundException("Hobby project not found.");
        var ownerView = string.Equals(project.OwnerUserId, currentUserId, StringComparison.Ordinal);
        if (!ownerView && !IsPublic(project))
        {
            throw new NotFoundException("Hobby project not found.");
        }

        return MapDetails(project, ownerView);
    }

    public Task<PagedResult<HobbyProjectEntryDto>> GetDiaryAsync(
        int projectId,
        string? currentUserId,
        int page,
        int pageSize) =>
        GetEntriesAsync(projectId, currentUserId, page, pageSize, showcaseOnly: false);

    public Task<PagedResult<HobbyProjectEntryDto>> GetShowcaseAsync(
        int projectId,
        string? currentUserId,
        int page,
        int pageSize) =>
        GetEntriesAsync(projectId, currentUserId, page, pageSize, showcaseOnly: true);

    public async Task<PagedResult<PostSummaryDto>> GetAvailablePostsAsync(
        string userId,
        int projectId,
        string? search,
        int page,
        int pageSize)
    {
        EnsureUserId(userId);
        PaginationGuard.Validate(page, pageSize);
        var normalizedSearch = NormalizeOptional(search, "search", HobbyProjectRules.MaxSearchLength);
        _ = await LoadOwnedProjectAsync(projectId, userId, asTracking: false);

        IQueryable<Post> posts = _dbContext.Posts
            .AsNoTracking()
            .Where(post =>
                post.CreatedById == userId
                && !post.IsDeleted
                && (post.HobbyProjectEntry == null || post.HobbyProjectEntry.ProjectId != projectId));
        if (normalizedSearch is not null)
        {
            posts = posts.Where(post => post.Title.Contains(normalizedSearch) || post.Content.Contains(normalizedSearch));
        }

        var totalCount = await posts.CountAsync();
        var pageIds = await posts
            .OrderByDescending(post => post.CreatedUtc)
            .ThenByDescending(post => post.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(post => post.Id)
            .ToListAsync();
        var graph = await PostGraph()
            .Where(post => pageIds.Contains(post.Id))
            .ToDictionaryAsync(post => post.Id);

        return new PagedResult<PostSummaryDto>
        {
            Items = pageIds
                .Where(graph.ContainsKey)
                .Select(id => MapPostSummary(graph[id]))
                .ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<HobbyProjectDto> CreateAsync(string userId, CreateHobbyProjectDto request)
    {
        EnsureUserId(userId);
        ArgumentNullException.ThrowIfNull(request);
        await EnsureProjectPublishingAllowedAsync(userId);
        var values = NormalizeProject(request.Title, request.Description, request.Kind, request.GameSystem, request.FactionTheme, request.Goal);

        if (await _dbContext.HobbyProjects.CountAsync(project => project.OwnerUserId == userId) >= HobbyProjectRules.MaxProjectsPerOwner)
        {
            throw new ConflictException($"A painter can have at most {HobbyProjectRules.MaxProjectsPerOwner} hobby projects.");
        }

        var owner = await _dbContext.Users.Include(user => user.Profile).FirstOrDefaultAsync(user => user.Id == userId)
            ?? throw new UnauthorizedAccessException("User must be authenticated to create projects.");
        var now = DateTime.UtcNow;
        var project = new HobbyProject
        {
            OwnerUserId = userId,
            OwnerUser = owner,
            Title = values.Title,
            Description = values.Description,
            Kind = values.Kind,
            GameSystem = values.GameSystem,
            FactionTheme = values.FactionTheme,
            Goal = values.Goal,
            StartDate = request.StartDate,
            Status = HobbyProjectStatuses.Planning,
            CreatedUtc = now,
            UpdatedUtc = now
        };
        _dbContext.HobbyProjects.Add(project);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Hobby project lifecycle outcome. Action=Created; ProjectId={ProjectId}.", project.Id);
        return await GetByIdAsync(project.Id, userId);
    }

    public async Task<HobbyProjectDto> UpdateAsync(string userId, int projectId, UpdateHobbyProjectDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var project = await LoadOwnedProjectAsync(projectId, userId);
        await EnsureOwnerNotSuspendedAsync(userId);
        EnsureActive(project);
        var values = NormalizeProject(request.Title, request.Description, request.Kind, request.GameSystem, request.FactionTheme, request.Goal);
        project.Title = values.Title;
        project.Description = values.Description;
        project.Kind = values.Kind;
        project.GameSystem = values.GameSystem;
        project.FactionTheme = values.FactionTheme;
        project.Goal = values.Goal;
        project.StartDate = request.StartDate;
        project.UpdatedUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Hobby project lifecycle outcome. Action=Updated; ProjectId={ProjectId}.", project.Id);
        return await GetByIdAsync(project.Id, userId);
    }

    public async Task<HobbyProjectDto> UpdateStatusAsync(string userId, int projectId, UpdateHobbyProjectStatusDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var project = await LoadOwnedProjectAsync(projectId, userId);
        await EnsureOwnerNotSuspendedAsync(userId);
        EnsureActive(project);
        var status = NormalizeChoice(request.Status, HobbyProjectStatuses.All, "status", "Provide a valid hobby project status.");
        var now = DateTime.UtcNow;
        if (status == HobbyProjectStatuses.Completed)
        {
            if (!project.Entries.Any(IsVisibleShowcaseEntry))
            {
                throw new ConflictException("A project needs at least one visible image-bearing showcase entry before completion.");
            }

            project.CompletedUtc ??= now;
        }
        else
        {
            project.CompletedUtc = null;
        }

        project.Status = status;
        project.UpdatedUtc = now;
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Hobby project lifecycle outcome. Action=StatusChanged; ProjectId={ProjectId}; Status={Status}.", project.Id, status);
        return await GetByIdAsync(project.Id, userId);
    }

    public async Task<HobbyProjectDto> ArchiveAsync(string userId, int projectId)
    {
        var project = await LoadOwnedProjectAsync(projectId, userId);
        if (project.ArchivedUtc is null)
        {
            project.ArchivedUtc = DateTime.UtcNow;
            project.UpdatedUtc = project.ArchivedUtc.Value;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Hobby project lifecycle outcome. Action=Archived; ProjectId={ProjectId}.", project.Id);
        }

        return await GetByIdAsync(project.Id, userId);
    }

    public async Task<HobbyProjectDto> RestoreAsync(string userId, int projectId)
    {
        var project = await LoadOwnedProjectAsync(projectId, userId);
        await EnsureProjectPublishingAllowedAsync(userId);
        if (project.ArchivedUtc is not null)
        {
            project.ArchivedUtc = null;
            project.UpdatedUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Hobby project lifecycle outcome. Action=Restored; ProjectId={ProjectId}.", project.Id);
        }

        return await GetByIdAsync(project.Id, userId);
    }

    public async Task<HobbyProjectDto> LinkPostAsync(string userId, int projectId, LinkHobbyProjectPostDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var project = await LoadOwnedProjectAsync(projectId, userId);
        await EnsureProjectPublishingAllowedAsync(userId);
        EnsureCanAddEntry(project);
        var milestone = NormalizeOptional(request.MilestoneLabel, "milestoneLabel", HobbyProjectRules.MaxMilestoneLabelLength);
        var post = await _dbContext.Posts
            .Include(item => item.Images)
            .Include(item => item.HobbyProjectEntry!)
            .ThenInclude(entry => entry.Project)
            .FirstOrDefaultAsync(item => item.Id == request.PostId && item.CreatedById == userId && !item.IsDeleted)
            ?? throw new NotFoundException("Post not found.");

        if (post.HobbyProjectEntry is not null)
        {
            var source = await LoadOwnedProjectAsync(post.HobbyProjectEntry.ProjectId, userId);
            if (source.Id == projectId)
            {
                throw LinkConflict("That post is already linked to this project.", source, post);
            }

            if (request.SourceProjectId != source.Id || source.OwnerUserId != userId)
            {
                throw LinkConflict("That post is already linked to another project. Confirm the move before trying again.", source, post);
            }

            var sourceEntry = source.Entries.First(entry => entry.PostId == post.Id);
            if (source.Status == HobbyProjectStatuses.Completed
                && IsVisibleShowcaseEntry(sourceEntry)
                && source.Entries.Count(IsVisibleShowcaseEntry) == 1)
            {
                throw new ConflictException("Reopen the source project before moving its final visible showcase entry.");
            }

            if (project.Entries.Count >= HobbyProjectRules.MaxEntriesPerProject)
            {
                throw new ConflictException($"A project can contain at most {HobbyProjectRules.MaxEntriesPerProject} posts.");
            }

            if (source.CoverPostId == post.Id)
            {
                source.CoverPostId = null;
            }

            var now = DateTime.UtcNow;
            sourceEntry.ProjectId = project.Id;
            sourceEntry.Project = project;
            sourceEntry.MilestoneLabel = milestone;
            sourceEntry.ShowcaseOrder = null;
            sourceEntry.LinkedUtc = now;
            source.UpdatedUtc = now;
            project.UpdatedUtc = now;
            var moveReopened = ReopenForNewEntry(project, now);
            await SaveLinkAsync(post.Id, project, post);
            _logger.LogInformation("Hobby project lifecycle outcome. Action=PostMoved; ProjectId={ProjectId}; SourceProjectId={SourceProjectId}; PostId={PostId}.", project.Id, source.Id, post.Id);
            LogReopened(project.Id, moveReopened);
            return await GetByIdAsync(project.Id, userId);
        }

        if (project.Entries.Count >= HobbyProjectRules.MaxEntriesPerProject)
        {
            throw new ConflictException($"A project can contain at most {HobbyProjectRules.MaxEntriesPerProject} posts.");
        }

        var wasEmpty = project.Entries.Count == 0;
        var linkedUtc = DateTime.UtcNow;
        var entry = new HobbyProjectEntry
        {
            Project = project,
            ProjectId = project.Id,
            Post = post,
            PostId = post.Id,
            LinkedUtc = linkedUtc,
            MilestoneLabel = milestone
        };
        project.Entries.Add(entry);
        post.HobbyProjectEntry = entry;
        project.UpdatedUtc = linkedUtc;
        var linkReopened = ReopenForNewEntry(project, linkedUtc);
        await SaveLinkAsync(post.Id, project, post);
        _logger.LogInformation("Hobby project lifecycle outcome. Action=PostLinked; ProjectId={ProjectId}; PostId={PostId}.", project.Id, post.Id);
        LogReopened(project.Id, linkReopened);
        if (wasEmpty)
        {
            _logger.LogInformation("Hobby project lifecycle outcome. Action=FirstEntryLinked; ProjectId={ProjectId}; PostId={PostId}.", project.Id, post.Id);
        }
        return await GetByIdAsync(project.Id, userId);
    }

    public async Task<HobbyProjectDto> UpdateEntryAsync(
        string userId,
        int projectId,
        int postId,
        UpdateHobbyProjectEntryDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var project = await LoadOwnedProjectAsync(projectId, userId);
        await EnsureOwnerNotSuspendedAsync(userId);
        EnsureActive(project);
        var entry = project.Entries.FirstOrDefault(item => item.PostId == postId)
            ?? throw new NotFoundException("Project post not found.");
        entry.MilestoneLabel = NormalizeOptional(request.MilestoneLabel, "milestoneLabel", HobbyProjectRules.MaxMilestoneLabelLength);
        project.UpdatedUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return await GetByIdAsync(project.Id, userId);
    }

    public async Task<HobbyProjectDto> UnlinkPostAsync(string userId, int projectId, int postId)
    {
        var project = await LoadOwnedProjectAsync(projectId, userId);
        var entry = project.Entries.FirstOrDefault(item => item.PostId == postId)
            ?? throw new NotFoundException("Project post not found.");
        var removesFinalCompletedShowcase = project.Status == HobbyProjectStatuses.Completed
            && entry.ShowcaseOrder.HasValue
            && project.Entries.Count(IsVisibleShowcaseEntry) == 1;
        if (removesFinalCompletedShowcase && project.ArchivedUtc is null)
        {
            throw new ConflictException("Reopen the project before removing its final visible showcase entry.");
        }

        if (project.CoverPostId == postId)
        {
            project.CoverPostId = null;
        }

        _dbContext.HobbyProjectEntries.Remove(entry);
        var now = DateTime.UtcNow;
        var reopened = removesFinalCompletedShowcase && ReopenForNewEntry(project, now);
        project.UpdatedUtc = now;
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Hobby project lifecycle outcome. Action=PostUnlinked; ProjectId={ProjectId}; PostId={PostId}.", project.Id, postId);
        LogReopened(project.Id, reopened);
        return await GetByIdAsync(project.Id, userId);
    }

    public async Task<HobbyProjectDto> UpdateShowcaseAsync(string userId, int projectId, UpdateHobbyProjectShowcaseDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var project = await LoadOwnedProjectAsync(projectId, userId);
        await EnsureOwnerNotSuspendedAsync(userId);
        EnsureActive(project);
        var postIds = request.PostIds ?? new List<int>();
        if (postIds.Count > HobbyProjectRules.MaxShowcaseEntries)
        {
            throw Validation("postIds", $"A showcase can contain at most {HobbyProjectRules.MaxShowcaseEntries} posts.");
        }

        if (postIds.Distinct().Count() != postIds.Count)
        {
            throw Validation("postIds", "Showcase posts must be distinct.");
        }

        var entryByPostId = project.Entries.ToDictionary(entry => entry.PostId);
        foreach (var postId in postIds)
        {
            if (!entryByPostId.TryGetValue(postId, out var entry) || entry.Post.IsDeleted || entry.Post.Images.Count == 0)
            {
                throw Validation("postIds", "Every showcase post must be a visible image-bearing diary entry.");
            }
        }

        if (project.Status == HobbyProjectStatuses.Completed && postIds.Count == 0)
        {
            throw new ConflictException("Reopen the project before removing its final showcase entry.");
        }

        var now = DateTime.UtcNow;
        var selectedOrders = postIds.Select((postId, index) => (postId, order: index + 1)).ToDictionary(item => item.postId, item => item.order);
        await ReplaceShowcaseOrderAsync(project.Id, selectedOrders, now);
        _logger.LogInformation("Hobby project lifecycle outcome. Action=ShowcaseUpdated; ProjectId={ProjectId}; EntryCount={EntryCount}.", project.Id, postIds.Count);
        return await GetByIdAsync(project.Id, userId);
    }

    public async Task<HobbyProjectDto> UpdateCoverAsync(string userId, int projectId, UpdateHobbyProjectCoverDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var project = await LoadOwnedProjectAsync(projectId, userId);
        await EnsureOwnerNotSuspendedAsync(userId);
        EnsureActive(project);
        if (request.PostId.HasValue)
        {
            var entry = project.Entries.FirstOrDefault(item => item.PostId == request.PostId.Value);
            if (entry is null || entry.Post.IsDeleted || entry.Post.Images.Count == 0)
            {
                throw Validation("postId", "The cover must be a visible image-bearing post linked to this project.");
            }
        }

        project.CoverPostId = request.PostId;
        project.UpdatedUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Hobby project lifecycle outcome. Action=CoverUpdated; ProjectId={ProjectId}.", project.Id);
        return await GetByIdAsync(project.Id, userId);
    }

    public async Task LinkNewPostAsync(string userId, Post post, int projectId, string? milestoneLabel)
    {
        EnsureUserId(userId);
        ArgumentNullException.ThrowIfNull(post);
        var project = await LoadOwnedProjectAsync(projectId, userId);
        EnsureCanAddEntry(project);
        if (project.Entries.Count >= HobbyProjectRules.MaxEntriesPerProject)
        {
            throw new ConflictException($"A project can contain at most {HobbyProjectRules.MaxEntriesPerProject} posts.");
        }

        var priorStatus = project.Status;
        _pendingPostRollbacks[post] = new ProjectRollbackState(
            project.Id,
            project.Status,
            project.CompletedUtc,
            project.UpdatedUtc,
            project.Entries.Count == 0,
            priorStatus == HobbyProjectStatuses.Completed);
        var now = DateTime.UtcNow;
        var entry = new HobbyProjectEntry
        {
            Project = project,
            Post = post,
            LinkedUtc = now,
            MilestoneLabel = NormalizeOptional(milestoneLabel, "milestoneLabel", HobbyProjectRules.MaxMilestoneLabelLength)
        };
        project.Entries.Add(entry);
        post.HobbyProjectEntry = entry;
        project.UpdatedUtc = now;
        ReopenForNewEntry(project, now);
    }

    public Task RollbackNewPostAsync(Post post)
    {
        ArgumentNullException.ThrowIfNull(post);
        if (!_pendingPostRollbacks.Remove(post, out var rollback) || post.HobbyProjectEntry?.Project is not HobbyProject project)
        {
            return Task.CompletedTask;
        }

        project.Status = rollback.Status;
        project.CompletedUtc = rollback.CompletedUtc;
        project.UpdatedUtc = rollback.UpdatedUtc;
        _logger.LogInformation("Hobby project lifecycle outcome. Action=FailedPostRolledBack; ProjectId={ProjectId}.", project.Id);
        return Task.CompletedTask;
    }

    private async Task<PagedResult<HobbyProjectSummaryDto>> GetProjectPageAsync(
        IQueryable<HobbyProject> query,
        ProjectFilter filter,
        bool ownerView)
    {
        var totalCount = await query.CountAsync();
        var ordered = filter.Sort switch
        {
            HobbyProjectSorts.Oldest => query.OrderBy(project => project.UpdatedUtc).ThenBy(project => project.Id),
            HobbyProjectSorts.Title => query.OrderBy(project => project.Title).ThenBy(project => project.Id),
            _ => query.OrderByDescending(project => project.UpdatedUtc).ThenByDescending(project => project.Id)
        };
        var ids = await ordered
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(project => project.Id)
            .ToListAsync();
        var graph = await ProjectGraph(asTracking: false)
            .Where(project => ids.Contains(project.Id))
            .ToDictionaryAsync(project => project.Id);

        return new PagedResult<HobbyProjectSummaryDto>
        {
            Items = ids.Where(graph.ContainsKey).Select(id => MapSummary(graph[id], ownerView)).ToList(),
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        };
    }

    private async Task<PagedResult<HobbyProjectEntryDto>> GetEntriesAsync(
        int projectId,
        string? currentUserId,
        int page,
        int pageSize,
        bool showcaseOnly)
    {
        PaginationGuard.Validate(page, pageSize);
        var project = await ProjectGraph(asTracking: false)
            .FirstOrDefaultAsync(item => item.Id == projectId)
            ?? throw new NotFoundException("Hobby project not found.");
        var ownerView = string.Equals(project.OwnerUserId, currentUserId, StringComparison.Ordinal);
        if (!ownerView && !IsPublic(project))
        {
            throw new NotFoundException("Hobby project not found.");
        }

        IEnumerable<HobbyProjectEntry> entries = project.Entries.Where(entry => !entry.Post.IsDeleted);

        entries = showcaseOnly
            ? entries.Where(entry => entry.ShowcaseOrder.HasValue).OrderBy(entry => entry.ShowcaseOrder).ThenBy(entry => entry.PostId)
            : entries.OrderByDescending(entry => entry.Post.CreatedUtc).ThenByDescending(entry => entry.PostId);
        var all = entries.ToList();
        return new PagedResult<HobbyProjectEntryDto>
        {
            Items = all.Skip((page - 1) * pageSize).Take(pageSize).Select(MapEntry).ToList(),
            TotalCount = all.Count,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    private IQueryable<HobbyProject> ProjectGraph(bool asTracking)
    {
        IQueryable<HobbyProject> query = _dbContext.HobbyProjects;
        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return query
            .Include(project => project.OwnerUser).ThenInclude(user => user.Profile)
            .Include(project => project.Entries).ThenInclude(entry => entry.Post).ThenInclude(post => post.CreatedBy).ThenInclude(user => user.Profile)
            .Include(project => project.Entries).ThenInclude(entry => entry.Post).ThenInclude(post => post.Images)
            .Include(project => project.Entries).ThenInclude(entry => entry.Post).ThenInclude(post => post.PostTags).ThenInclude(postTag => postTag.Tag)
            .Include(project => project.Entries).ThenInclude(entry => entry.Post).ThenInclude(post => post.Comments)
            .Include(project => project.Entries).ThenInclude(entry => entry.Post).ThenInclude(post => post.Likes)
            .AsSplitQuery();
    }

    private IQueryable<Post> PostGraph() =>
        _dbContext.Posts
            .AsNoTracking()
            .Include(post => post.CreatedBy).ThenInclude(user => user.Profile)
            .Include(post => post.Images)
            .Include(post => post.PostTags).ThenInclude(postTag => postTag.Tag)
            .Include(post => post.Comments)
            .Include(post => post.Likes)
            .Include(post => post.HobbyProjectEntry!).ThenInclude(entry => entry.Project)
            .AsSplitQuery();

    private async Task<HobbyProject> LoadOwnedProjectAsync(int projectId, string userId, bool asTracking = true)
    {
        EnsureUserId(userId);
        var project = await ProjectGraph(asTracking)
            .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId);
        return project ?? throw new NotFoundException("Hobby project not found.");
    }

    private async Task ReplaceShowcaseOrderAsync(int projectId, IReadOnlyDictionary<int, int> selectedOrders, DateTime updatedUtc)
    {
        _dbContext.ChangeTracker.Clear();
        if (!_dbContext.Database.IsRelational())
        {
            var entries = await _dbContext.HobbyProjectEntries.Where(entry => entry.ProjectId == projectId).ToListAsync();
            foreach (var entry in entries)
            {
                entry.ShowcaseOrder = null;
            }

            await _dbContext.SaveChangesAsync();
            foreach (var entry in entries)
            {
                entry.ShowcaseOrder = selectedOrders.TryGetValue(entry.PostId, out var order) ? order : null;
            }

            var project = await _dbContext.HobbyProjects.FindAsync(projectId) ?? throw new NotFoundException("Hobby project not found.");
            project.UpdatedUtc = updatedUtc;
            await _dbContext.SaveChangesAsync();
            return;
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var entries = await _dbContext.HobbyProjectEntries.Where(entry => entry.ProjectId == projectId).ToListAsync();
            foreach (var entry in entries)
            {
                entry.ShowcaseOrder = null;
            }

            await _dbContext.SaveChangesAsync();
            foreach (var entry in entries)
            {
                entry.ShowcaseOrder = selectedOrders.TryGetValue(entry.PostId, out var order) ? order : null;
            }

            var project = await _dbContext.HobbyProjects.FindAsync(projectId) ?? throw new NotFoundException("Hobby project not found.");
            project.UpdatedUtc = updatedUtc;
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }

    private async Task SaveLinkAsync(int postId, HobbyProject attemptedProject, Post post)
    {
        var fallbackProject = MapLinkReference(attemptedProject, post.IsDeleted);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsLikelyUniqueConstraint(ex))
        {
            _dbContext.ChangeTracker.Clear();
            var currentProject = fallbackProject;
            try
            {
                currentProject = await _dbContext.HobbyProjectEntries
                    .AsNoTracking()
                    .Where(entry => entry.PostId == postId)
                    .Select(entry => new HobbyProjectReferenceDto
                    {
                        Id = entry.Project.Id,
                        Title = entry.Project.Title,
                        Status = entry.Project.Status,
                        IsPublic = !entry.Project.IsHidden
                            && entry.Project.ArchivedUtc == null
                            && !entry.Post.IsDeleted
                    })
                    .FirstOrDefaultAsync()
                    ?? fallbackProject;
            }
            catch (Exception recoveryException)
            {
                _logger.LogWarning(
                    recoveryException,
                    "Hobby project link conflict recovery failed. PostId={PostId}; FallbackProjectId={ProjectId}.",
                    postId,
                    fallbackProject.Id);
            }

            throw new HobbyProjectLinkConflictException(
                "That post is already linked to a hobby project.",
                currentProject);
        }
    }

    private static bool IsLikelyUniqueConstraint(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static HobbyProjectDto MapDetails(HobbyProject project, bool ownerView)
    {
        var summary = MapSummary(project, ownerView);
        return new HobbyProjectDto
        {
            Id = summary.Id,
            OwnerUserId = summary.OwnerUserId,
            OwnerUserName = summary.OwnerUserName,
            OwnerDisplayName = summary.OwnerDisplayName,
            OwnerAvatarUrl = summary.OwnerAvatarUrl,
            Title = summary.Title,
            Description = summary.Description,
            Kind = summary.Kind,
            GameSystem = summary.GameSystem,
            FactionTheme = summary.FactionTheme,
            Goal = summary.Goal,
            StartDate = summary.StartDate,
            Status = summary.Status,
            SelectedCoverPostId = summary.SelectedCoverPostId,
            CoverPostId = summary.CoverPostId,
            CoverImageUrl = summary.CoverImageUrl,
            CoverThumbnailUrl = summary.CoverThumbnailUrl,
            EntryCount = summary.EntryCount,
            ShowcaseCount = summary.ShowcaseCount,
            CreatedUtc = summary.CreatedUtc,
            UpdatedUtc = summary.UpdatedUtc,
            CompletedUtc = summary.CompletedUtc,
            ArchivedUtc = summary.ArchivedUtc,
            IsArchived = summary.IsArchived,
            IsHidden = summary.IsHidden,
            IsPublic = summary.IsPublic,
            HasCurationWarning = summary.HasCurationWarning,
            ModeratedUtc = ownerView ? project.ModeratedUtc : null,
            ModerationReason = ownerView ? project.ModerationReason : null
        };
    }

    internal static HobbyProjectSummaryDto MapSummary(HobbyProject project, bool ownerView)
    {
        var visibleEntries = project.Entries.Where(entry => !entry.Post.IsDeleted).ToList();
        var coverPost = ResolveCoverPost(project, visibleEntries);
        var coverImage = coverPost?.Images.OrderBy(image => image.Id).FirstOrDefault();
        return new HobbyProjectSummaryDto
        {
            Id = project.Id,
            OwnerUserId = project.OwnerUserId,
            OwnerUserName = project.OwnerUser.UserName ?? string.Empty,
            OwnerDisplayName = project.OwnerUser.Profile?.DisplayName ?? project.OwnerUser.DisplayName ?? project.OwnerUser.UserName ?? project.OwnerUserId,
            OwnerAvatarUrl = project.OwnerUser.Profile?.AvatarUrl ?? project.OwnerUser.AvatarUrl,
            Title = project.Title,
            Description = project.Description,
            Kind = project.Kind,
            GameSystem = project.GameSystem,
            FactionTheme = project.FactionTheme,
            Goal = project.Goal,
            StartDate = project.StartDate,
            Status = project.Status,
            SelectedCoverPostId = ownerView ? project.CoverPostId : null,
            CoverPostId = coverPost?.Id,
            CoverImageUrl = coverImage?.ImageUrl,
            CoverThumbnailUrl = coverImage?.ThumbnailUrl ?? coverImage?.PreviewUrl ?? coverImage?.ImageUrl,
            EntryCount = visibleEntries.Count,
            ShowcaseCount = visibleEntries.Count(entry => entry.ShowcaseOrder.HasValue),
            CreatedUtc = project.CreatedUtc,
            UpdatedUtc = project.UpdatedUtc,
            CompletedUtc = project.CompletedUtc,
            ArchivedUtc = project.ArchivedUtc,
            IsArchived = project.ArchivedUtc.HasValue,
            IsHidden = project.IsHidden,
            IsPublic = IsPublic(project),
            HasCurationWarning = ownerView
                && project.Status == HobbyProjectStatuses.Completed
                && !visibleEntries.Any(entry => entry.ShowcaseOrder.HasValue && entry.Post.Images.Count > 0)
        };
    }

    private static Post? ResolveCoverPost(HobbyProject project, IReadOnlyCollection<HobbyProjectEntry> visibleEntries)
    {
        var selected = visibleEntries.FirstOrDefault(entry => entry.PostId == project.CoverPostId && entry.Post.Images.Count > 0)?.Post;
        if (selected is not null)
        {
            return selected;
        }

        return visibleEntries
            .Where(entry => entry.ShowcaseOrder.HasValue && entry.Post.Images.Count > 0)
            .OrderBy(entry => entry.ShowcaseOrder)
            .Select(entry => entry.Post)
            .FirstOrDefault()
            ?? visibleEntries
                .Where(entry => entry.Post.Images.Count > 0)
                .OrderByDescending(entry => entry.Post.CreatedUtc)
                .ThenByDescending(entry => entry.PostId)
                .Select(entry => entry.Post)
                .FirstOrDefault();
    }

    private static HobbyProjectEntryDto MapEntry(HobbyProjectEntry entry) =>
        new()
        {
            Id = entry.Id,
            ProjectId = entry.ProjectId,
            PostId = entry.PostId,
            LinkedUtc = entry.LinkedUtc,
            MilestoneLabel = entry.MilestoneLabel,
            ShowcaseOrder = entry.ShowcaseOrder,
            Post = MapPostSummary(entry.Post)
        };

    private static PostSummaryDto MapPostSummary(Post post) =>
        PostDtoMapper.ToPostSummaryDto(
            post,
            post.Comments.Count(comment => !comment.IsDeleted),
            post.Likes.Count);

    private static bool IsPublic(HobbyProject project) =>
        !project.IsHidden
        && project.ArchivedUtc is null
        && project.Entries.Any(entry => !entry.Post.IsDeleted);

    private static bool IsVisibleShowcaseEntry(HobbyProjectEntry entry) =>
        entry.ShowcaseOrder.HasValue && !entry.Post.IsDeleted && entry.Post.Images.Count > 0;

    private static void EnsureCanAddEntry(HobbyProject project)
    {
        EnsureActive(project);
        if (project.IsHidden)
        {
            throw new ConflictException("Staff must restore this project before new posts can be added.");
        }
    }

    private static void EnsureActive(HobbyProject project)
    {
        if (project.ArchivedUtc.HasValue)
        {
            throw new ConflictException("Restore the project before changing it.");
        }
    }

    private static bool ReopenForNewEntry(HobbyProject project, DateTime now)
    {
        if (project.Status != HobbyProjectStatuses.Completed)
        {
            return false;
        }

        project.Status = HobbyProjectStatuses.InProgress;
        project.CompletedUtc = null;
        project.UpdatedUtc = now;
        return true;
    }

    private void LogReopened(int projectId, bool reopened)
    {
        if (reopened)
        {
            _logger.LogInformation(
                "Hobby project lifecycle outcome. Action=Reopened; ProjectId={ProjectId}; Status={Status}.",
                projectId,
                HobbyProjectStatuses.InProgress);
        }
    }

    private void LogCommittedNewPostLinks()
    {
        foreach (var pending in _pendingPostRollbacks)
        {
            var post = pending.Key;
            var state = pending.Value;
            var entry = post.HobbyProjectEntry;
            if (state.SuccessLogged
                || post.Id <= 0
                || entry is null
                || _dbContext.Entry(post).State != EntityState.Unchanged
                || _dbContext.Entry(entry).State != EntityState.Unchanged)
            {
                continue;
            }

            state.SuccessLogged = true;
            _logger.LogInformation(
                "Hobby project lifecycle outcome. Action=NewPostLinked; ProjectId={ProjectId}; PostId={PostId}.",
                state.ProjectId,
                post.Id);
            if (state.WasFirstEntry)
            {
                _logger.LogInformation(
                    "Hobby project lifecycle outcome. Action=FirstEntryLinked; ProjectId={ProjectId}; PostId={PostId}.",
                    state.ProjectId,
                    post.Id);
            }

            LogReopened(state.ProjectId, state.WasReopened);
        }
    }

    private async Task EnsureProjectPublishingAllowedAsync(string userId)
    {
        EnsureUserId(userId);
        if (_accountRestrictionService is not null)
        {
            await _accountRestrictionService.EnsureCanCreatePostAsync(userId);
        }
    }

    private async Task EnsureOwnerNotSuspendedAsync(string userId)
    {
        EnsureUserId(userId);
        if (_accountRestrictionService is null)
        {
            return;
        }

        var owner = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId)
            ?? throw new UnauthorizedAccessException("User must be authenticated to manage projects.");

        await _accountRestrictionService.EnsureCanLoginAsync(owner);
    }

    private static ProjectValues NormalizeProject(
        string? title,
        string? description,
        string? kind,
        string? gameSystem,
        string? factionTheme,
        string? goal) =>
        new(
            NormalizeRequired(title, "title", HobbyProjectRules.MaxTitleLength),
            NormalizeRequired(description, "description", HobbyProjectRules.MaxDescriptionLength),
            NormalizeChoice(kind, HobbyProjectKinds.All, "kind", "Provide a valid hobby project kind."),
            NormalizeOptional(gameSystem, "gameSystem", HobbyProjectRules.MaxGameSystemLength),
            NormalizeOptional(factionTheme, "factionTheme", HobbyProjectRules.MaxFactionThemeLength),
            NormalizeOptional(goal, "goal", HobbyProjectRules.MaxGoalLength));

    private static ProjectFilter ValidateQuery(HobbyProjectQueryDto query)
    {
        PaginationGuard.Validate(query.PageNumber, query.PageSize, "Hobby project query is invalid.");
        return new ProjectFilter(
            NormalizeOptional(query.Search, "search", HobbyProjectRules.MaxSearchLength),
            NormalizeOptional(query.OwnerUserId, "ownerUserId", 450),
            NormalizeOptionalChoice(query.Kind, HobbyProjectKinds.All, "kind", "Provide a valid hobby project kind."),
            NormalizeOptionalChoice(query.Status, HobbyProjectStatuses.All, "status", "Provide a valid hobby project status."),
            NormalizeChoice(query.Sort, HobbyProjectSorts.All, "sort", "Provide a valid hobby project sort."),
            query.IncludeArchived,
            query.PageNumber,
            query.PageSize);
    }

    private static IQueryable<HobbyProject> ApplyFilters(
        IQueryable<HobbyProject> query,
        ProjectFilter filter,
        bool includeArchiveFilter = false)
    {
        if (filter.Search is not null)
        {
            query = query.Where(project =>
                project.Title.Contains(filter.Search)
                || project.Description.Contains(filter.Search)
                || (project.GameSystem != null && project.GameSystem.Contains(filter.Search))
                || (project.FactionTheme != null && project.FactionTheme.Contains(filter.Search))
                || (project.Goal != null && project.Goal.Contains(filter.Search))
                || (project.OwnerUser.UserName != null && project.OwnerUser.UserName.Contains(filter.Search))
                || (project.OwnerUser.DisplayName != null && project.OwnerUser.DisplayName.Contains(filter.Search))
                || (project.OwnerUser.Profile != null && project.OwnerUser.Profile.DisplayName != null && project.OwnerUser.Profile.DisplayName.Contains(filter.Search)));
        }

        if (filter.OwnerUserId is not null)
        {
            query = query.Where(project => project.OwnerUserId == filter.OwnerUserId);
        }

        if (filter.Kind is not null)
        {
            query = query.Where(project => project.Kind == filter.Kind);
        }

        if (filter.Status is not null)
        {
            query = query.Where(project => project.Status == filter.Status);
        }

        if (includeArchiveFilter && !filter.IncludeArchived)
        {
            query = query.Where(project => project.ArchivedUtc == null);
        }

        return query;
    }

    private static string NormalizeRequired(string? value, string key, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw Validation(key, $"{key} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw Validation(key, $"{key} must be {maxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, string key, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw Validation(key, $"{key} must be {maxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string NormalizeChoice(string? value, IReadOnlyList<string> choices, string key, string message)
    {
        var canonical = choices.FirstOrDefault(choice => string.Equals(choice, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return canonical ?? throw Validation(key, message);
    }

    private static string? NormalizeOptionalChoice(string? value, IReadOnlyList<string> choices, string key, string message) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeChoice(value, choices, key, message);

    private static DomainValidationException Validation(string key, string message) =>
        new("Hobby project data is invalid.", new Dictionary<string, string[]> { [key] = new[] { message } });

    private static HobbyProjectLinkConflictException LinkConflict(string message, HobbyProject project, Post post) =>
        new(message, MapLinkReference(project, post.IsDeleted));

    private static HobbyProjectReferenceDto MapLinkReference(HobbyProject project, bool postIsDeleted) =>
        new()
        {
            Id = project.Id,
            Title = project.Title,
            Status = project.Status,
            IsPublic = !project.IsHidden && project.ArchivedUtc is null && !postIsDeleted
        };

    private static void EnsureUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("User must be authenticated to manage hobby projects.");
        }
    }

    private sealed record ProjectValues(
        string Title,
        string Description,
        string Kind,
        string? GameSystem,
        string? FactionTheme,
        string? Goal);

    private sealed record ProjectFilter(
        string? Search,
        string? OwnerUserId,
        string? Kind,
        string? Status,
        string Sort,
        bool IncludeArchived,
        int PageNumber,
        int PageSize);

    private sealed class ProjectRollbackState
    {
        public ProjectRollbackState(
            int projectId,
            string status,
            DateTime? completedUtc,
            DateTime updatedUtc,
            bool wasFirstEntry,
            bool wasReopened)
        {
            ProjectId = projectId;
            Status = status;
            CompletedUtc = completedUtc;
            UpdatedUtc = updatedUtc;
            WasFirstEntry = wasFirstEntry;
            WasReopened = wasReopened;
        }

        public int ProjectId { get; }
        public string Status { get; }
        public DateTime? CompletedUtc { get; }
        public DateTime UpdatedUtc { get; }
        public bool WasFirstEntry { get; }
        public bool WasReopened { get; }
        public bool SuccessLogged { get; set; }
    }
}

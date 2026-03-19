using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Tests.Infrastructure;

internal sealed class StubAuthService : IAuthService
{
    public Func<LoginDto, Task<bool>> LoginHandler { get; set; } = _ => Task.FromResult(true);
    public Func<RegisterDto, Task<bool>> RegisterHandler { get; set; } = _ => Task.FromResult(true);
    public Func<Task> LogoutHandler { get; set; } = () => Task.CompletedTask;

    public Task<bool> LoginAsync(LoginDto dto) => LoginHandler(dto);
    public Task<bool> RegisterAsync(RegisterDto dto) => RegisterHandler(dto);
    public Task LogoutAsync() => LogoutHandler();
}

internal sealed class StubProfileService : IProfileService
{
    public Func<Task<UserProfileDto?>> GetMineHandler { get; set; } = () => Task.FromResult<UserProfileDto?>(null);
    public Func<CreateUserProfileDto, Task<UserProfileDto>> CreateMineHandler { get; set; } = dto => Task.FromResult(new UserProfileDto
    {
        UserId = "stub-user",
        DisplayName = dto.DisplayName,
        Bio = dto.Bio
    });
    public Func<UpdateUserProfileDto, Task<UserProfileDto>> UpdateMineHandler { get; set; } = dto => Task.FromResult(new UserProfileDto
    {
        UserId = "stub-user",
        DisplayName = dto.DisplayName,
        Bio = dto.Bio
    });
    public Func<IBrowserFile, long, Task<UserProfileDto>> UploadAvatarHandler { get; set; } = (_, _) => Task.FromResult(new UserProfileDto
    {
        UserId = "stub-user",
        DisplayName = "Stub User",
        AvatarUrl = "/images/avatar.png"
    });
    public Func<Task<UserProfileDto>> RemoveAvatarHandler { get; set; } = () => Task.FromResult(new UserProfileDto
    {
        UserId = "stub-user",
        DisplayName = "Stub User",
        AvatarUrl = null
    });
    public Func<string, Task<PublicUserProfileDto>> GetPublicByIdHandler { get; set; } = id => Task.FromResult(new PublicUserProfileDto
    {
        UserId = id,
        DisplayName = $"User {id}"
    });

    public Task<UserProfileDto?> GetMineAsync() => GetMineHandler();
    public Task<UserProfileDto> CreateMineAsync(CreateUserProfileDto dto) => CreateMineHandler(dto);
    public Task<UserProfileDto> UpdateMineAsync(UpdateUserProfileDto dto) => UpdateMineHandler(dto);
    public Task<UserProfileDto> UploadAvatarAsync(IBrowserFile file, long maxSizeBytes = 5_000_000) => UploadAvatarHandler(file, maxSizeBytes);
    public Task<UserProfileDto> RemoveAvatarAsync() => RemoveAvatarHandler();
    public Task<PublicUserProfileDto> GetPublicProfileById(string id) => GetPublicByIdHandler(id);
}

internal sealed class StubFollowService : IFollowService
{
    public Func<string, Task> FollowHandler { get; set; } = _ => Task.CompletedTask;
    public Func<string, Task> UnfollowHandler { get; set; } = _ => Task.CompletedTask;
    public Func<Task<IReadOnlyList<UserListItemDto>>> GetFollowersHandler { get; set; } = () =>
        Task.FromResult<IReadOnlyList<UserListItemDto>>(Array.Empty<UserListItemDto>());
    public Func<Task<IReadOnlyList<UserListItemDto>>> GetFollowingHandler { get; set; } = () =>
        Task.FromResult<IReadOnlyList<UserListItemDto>>(Array.Empty<UserListItemDto>());

    public Task FollowAsync(string userId) => FollowHandler(userId);
    public Task UnfollowAsync(string userId) => UnfollowHandler(userId);
    public Task<IReadOnlyList<UserListItemDto>> GetFollowersAsync() => GetFollowersHandler();
    public Task<IReadOnlyList<UserListItemDto>> GetFollowingAsync() => GetFollowingHandler();
}

internal sealed class StubConversationService : IConversationService
{
    private IReadOnlyList<ConversationSummaryDto> _conversations = Array.Empty<ConversationSummaryDto>();

    public event Action? OnChange;
    public event Action<DirectMessageDto>? MessageReceived;
    public event Action<ConversationReadDto>? ConversationRead;

    public Func<bool, Task<IReadOnlyList<ConversationSummaryDto>>> GetConversationsHandler { get; set; } = _ =>
        Task.FromResult<IReadOnlyList<ConversationSummaryDto>>(Array.Empty<ConversationSummaryDto>());
    public Func<string, Task<ConversationSummaryDto>> OpenDirectConversationHandler { get; set; } = userId =>
        Task.FromResult(new ConversationSummaryDto
        {
            Id = 1,
            OtherUser = new UserListItemDto { UserId = userId, UserName = userId, DisplayName = userId }
        });
    public Func<int, int?, int, Task<PagedResult<DirectMessageDto>>> GetMessagesHandler { get; set; } = (_, _, pageSize) =>
        Task.FromResult(new PagedResult<DirectMessageDto> { Items = new List<DirectMessageDto>(), PageNumber = 1, PageSize = pageSize, TotalCount = 0 });
    public Func<int, CreateDirectMessageDto, Task<DirectMessageDto>> SendMessageHandler { get; set; } = (conversationId, dto) =>
        Task.FromResult(new DirectMessageDto { Id = 1, ConversationId = conversationId, Body = dto.Body, SenderUserId = "stub-user", SenderDisplayName = "Stub User", IsMine = true, SentUtc = DateTime.UtcNow });
    public Func<int, Task> MarkReadHandler { get; set; } = _ => Task.CompletedTask;
    public Func<Task> EnsureRealtimeHandler { get; set; } = () => Task.CompletedTask;
    public Func<int, Task> JoinConversationHandler { get; set; } = _ => Task.CompletedTask;
    public Func<int, Task> LeaveConversationHandler { get; set; } = _ => Task.CompletedTask;

    public int UnreadConversationCount => _conversations.Count(c => c.UnreadCount > 0);

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _conversations.Count > 0)
        {
            return _conversations;
        }

        _conversations = await GetConversationsHandler(forceRefresh);
        OnChange?.Invoke();
        return _conversations;
    }

    public Task<ConversationSummaryDto> OpenDirectConversationAsync(string userId) => OpenDirectConversationHandler(userId);
    public Task<PagedResult<DirectMessageDto>> GetMessagesAsync(int conversationId, int? beforeMessageId = null, int pageSize = 50) => GetMessagesHandler(conversationId, beforeMessageId, pageSize);
    public Task<DirectMessageDto> SendMessageAsync(int conversationId, CreateDirectMessageDto dto) => SendMessageHandler(conversationId, dto);
    public Task MarkReadAsync(int conversationId) => MarkReadHandler(conversationId);
    public Task EnsureRealtimeAsync() => EnsureRealtimeHandler();
    public Task JoinConversationAsync(int conversationId) => JoinConversationHandler(conversationId);
    public Task LeaveConversationAsync(int conversationId) => LeaveConversationHandler(conversationId);

    public void SetConversations(IReadOnlyList<ConversationSummaryDto> conversations)
    {
        _conversations = conversations;
    }

    public void RaiseChanged() => OnChange?.Invoke();
    public void RaiseMessageReceived(DirectMessageDto dto) => MessageReceived?.Invoke(dto);
    public void RaiseConversationRead(ConversationReadDto dto) => ConversationRead?.Invoke(dto);
}

internal sealed class StubPostService : IPostService
{
    public Func<int, int, Task<ApiResult<PagedResult<PostSummaryDto>>>> GetAllHandler { get; set; } = (_, _) =>
        Task.FromResult(new ApiResult<PagedResult<PostSummaryDto>>(true, HttpStatusCode.OK, new PagedResult<PostSummaryDto>()));

    public Func<int, int, bool, bool, Task<ApiResult<PagedResult<PostSummaryDto>>>> GetAllWithVisibilityHandler { get; set; }

    public Func<string, int, int, Task<ApiResult<PagedResult<PostSummaryDto>>>> GetByAuthorHandler { get; set; } = (_, _, _) =>
        Task.FromResult(new ApiResult<PagedResult<PostSummaryDto>>(true, HttpStatusCode.OK, new PagedResult<PostSummaryDto>()));

    public Func<int, int, Task<ApiResult<PagedResult<PostSummaryDto>>>> GetFollowingFeedHandler { get; set; } = (_, _) =>
        Task.FromResult(new ApiResult<PagedResult<PostSummaryDto>>(true, HttpStatusCode.OK, new PagedResult<PostSummaryDto>()));

    public Func<int, Task<PostDto>> GetByIdHandler { get; set; } = id =>
        Task.FromResult(new PostDto { Id = id, Title = $"Post {id}", Content = "Stub content", CreatedById = "stub-user" });

    public Func<CreatePostDto, Task<PostDto>> CreateHandler { get; set; } = dto =>
        Task.FromResult(new PostDto { Id = 42, Title = dto.Title, Content = dto.Content, CreatedById = "stub-user" });

    public Func<MultipartFormDataContent, Task<PostDto>> CreateWithImageHandler { get; set; } = _ =>
        Task.FromResult(new PostDto { Id = 43, Title = "Uploaded", Content = "Uploaded content", CreatedById = "stub-user" });

    public Func<int, TimeSpan, Task<IEnumerable<PostSummaryDto>>> GetTopPostsHandler { get; set; } = (_, _) =>
        Task.FromResult<IEnumerable<PostSummaryDto>>(Array.Empty<PostSummaryDto>());

    public StubPostService()
    {
        GetAllWithVisibilityHandler = (page, pageSize, _, _) => GetAllHandler(page, pageSize);
    }

    public Task<ApiResult<PagedResult<PostSummaryDto>>> GetAllAsync(int page, int pageSize, bool includeDeleted = false, bool deletedOnly = false) =>
        GetAllWithVisibilityHandler(page, pageSize, includeDeleted, deletedOnly);
    public Task<ApiResult<PagedResult<PostSummaryDto>>> GetByAuthorAsync(string userId, int page, int pageSize) => GetByAuthorHandler(userId, page, pageSize);
    public Task<ApiResult<PagedResult<PostSummaryDto>>> GetFollowingFeedAsync(int page, int pageSize) => GetFollowingFeedHandler(page, pageSize);
    public Task<IEnumerable<PostSummaryDto>> GetTopPosts(int count, TimeSpan timeOffcet) => GetTopPostsHandler(count, timeOffcet);
    public Task<PostDto> GetByIdAsync(int id) => GetByIdHandler(id);
    public Task<PostDto> CreateAsync(CreatePostDto dto) => CreateHandler(dto);
    public Task<PostDto> CreateWithImageAsync(MultipartFormDataContent content) => CreateWithImageHandler(content);
}

internal sealed class StubPostViewerService : IPostViewerService
{
    public Func<int, Task<PostViewerDto>> GetHandler { get; set; } = postId =>
        Task.FromResult(new PostViewerDto
        {
            PostId = postId,
            Title = $"Post {postId}",
            CreatedById = "stub-user",
            AuthorName = "Stub User",
            CreatedAt = DateTime.UtcNow,
            CanAttachCommentMark = true,
            Images =
            {
                new PostViewerImageDto
                {
                    Id = 1,
                    ImageUrl = "/images/test_max.png",
                    PreviewUrl = "/images/test_preview.png",
                    ThumbnailUrl = "/images/test_thumb.png",
                    Width = 1600,
                    Height = 900
                }
            }
        });

    public Task<PostViewerDto> GetAsync(int postId) => GetHandler(postId);
}

internal sealed class StubAuthorMarkService : IAuthorMarkService
{
    public Func<int, int, CreateAuthorMarkDto, Task<AuthorMarkDto>> CreateHandler { get; set; } = (_, imageId, dto) =>
        Task.FromResult(new AuthorMarkDto
        {
            Id = 1,
            PostImageId = imageId,
            NormalizedX = dto.NormalizedX,
            NormalizedY = dto.NormalizedY,
            Tag = dto.Tag,
            Message = dto.Message
        });

    public Func<int, UpdateAuthorMarkDto, Task<AuthorMarkDto>> UpdateHandler { get; set; } = (markId, dto) =>
        Task.FromResult(new AuthorMarkDto
        {
            Id = markId,
            PostImageId = 1,
            NormalizedX = dto.NormalizedX,
            NormalizedY = dto.NormalizedY,
            Tag = dto.Tag,
            Message = dto.Message
        });

    public Func<int, Task> DeleteHandler { get; set; } = _ => Task.CompletedTask;

    public Task<AuthorMarkDto> CreateAsync(int postId, int imageId, CreateAuthorMarkDto dto) => CreateHandler(postId, imageId, dto);
    public Task<AuthorMarkDto> UpdateAsync(int markId, UpdateAuthorMarkDto dto) => UpdateHandler(markId, dto);
    public Task DeleteAsync(int markId) => DeleteHandler(markId);
}

internal sealed class StubCommentMarkService : ICommentMarkService
{
    public Func<int, bool, Task<CommentMarkDto>> GetByCommentIdHandler { get; set; } = (commentId, _) =>
        Task.FromResult(new CommentMarkDto
        {
            CommentId = commentId,
            PostImageId = 1,
            NormalizedX = 0.5m,
            NormalizedY = 0.5m
        });

    public Func<int, ViewerMarkDraftDto, Task<CommentMarkDto>> UpsertHandler { get; set; } = (commentId, dto) =>
        Task.FromResult(new CommentMarkDto
        {
            CommentId = commentId,
            PostImageId = dto.PostImageId,
            NormalizedX = dto.NormalizedX,
            NormalizedY = dto.NormalizedY
        });

    public Func<int, Task> DeleteHandler { get; set; } = _ => Task.CompletedTask;

    public Task<CommentMarkDto> GetByCommentIdAsync(int commentId, bool includeDeleted = false) => GetByCommentIdHandler(commentId, includeDeleted);
    public Task<CommentMarkDto> UpsertAsync(int commentId, ViewerMarkDraftDto dto) => UpsertHandler(commentId, dto);
    public Task DeleteAsync(int commentId) => DeleteHandler(commentId);
}

internal sealed class StubCommentService : ICommentService
{
    public Func<int, int, int, Task<ApiResult<PagedResult<CommentDto>>>> GetByPostHandler { get; set; } = (_, _, _) =>
        Task.FromResult(new ApiResult<PagedResult<CommentDto>>(true, HttpStatusCode.OK, new PagedResult<CommentDto>()));

    public Func<int, int, int, bool, bool, Task<ApiResult<PagedResult<CommentDto>>>> GetByPostWithVisibilityHandler { get; set; } = (_, _, _, _, _) =>
        Task.FromResult(new ApiResult<PagedResult<CommentDto>>(true, HttpStatusCode.OK, new PagedResult<CommentDto>()));

    public Func<int, CreateCommentDto, Task<ApiResult<CommentDto?>>> CreateHandler { get; set; } = (postId, dto) =>
        Task.FromResult(new ApiResult<CommentDto?>(true, HttpStatusCode.Created, new CommentDto
        {
            Id = 1,
            PostId = postId,
            Content = dto.Text,
            AuthorId = "stub-user",
            AuthorName = "stub-user"
        }));

    public StubCommentService()
    {
        GetByPostWithVisibilityHandler = (postId, page, pageSize, _, _) => GetByPostHandler(postId, page, pageSize);
    }

    public Task<ApiResult<PagedResult<CommentDto>>> GetByPostAsync(int postId, int page, int pageSize, bool includeDeleted = false, bool deletedOnly = false) =>
        GetByPostWithVisibilityHandler(postId, page, pageSize, includeDeleted, deletedOnly);
    public Task<ApiResult<CommentDto?>> CreateAsync(int postId, CreateCommentDto dto) => CreateHandler(postId, dto);
}

internal sealed class StubLikeService : ILikeService
{
    public Func<int, Task<LikeDto>> GetLikesHandler { get; set; } = _ => Task.FromResult(new LikeDto { Count = 0, UserHasLiked = false });
    public Func<int, Task<LikeDto>> ToggleLikeHandler { get; set; } = _ => Task.FromResult(new LikeDto { Count = 1, UserHasLiked = true });

    public Task<LikeDto> GetLikesAsync(int postId) => GetLikesHandler(postId);
    public Task<LikeDto> ToggleLikeAsync(int postId) => ToggleLikeHandler(postId);
}

internal sealed class StubModerationService : IModerationService
{
    public Func<int, ModerationActionRequestDto, Task<bool>> HidePostHandler { get; set; } = (_, _) => Task.FromResult(true);
    public Func<int, ModerationActionRequestDto, Task<bool>> RestorePostHandler { get; set; } = (_, _) => Task.FromResult(true);
    public Func<int, ModerationActionRequestDto, Task<bool>> HideCommentHandler { get; set; } = (_, _) => Task.FromResult(true);
    public Func<int, ModerationActionRequestDto, Task<bool>> RestoreCommentHandler { get; set; } = (_, _) => Task.FromResult(true);
    public Func<string, SuspendUserRequestDto, Task<bool>> SuspendUserHandler { get; set; } = (_, _) => Task.FromResult(true);
    public Func<string, ModerationActionRequestDto, Task<bool>> UnsuspendUserHandler { get; set; } = (_, _) => Task.FromResult(true);
    public Func<ModerationAuditQueryDto, Task<ApiResult<PagedResult<ModerationAuditDto>?>>> GetAuditHandler { get; set; } = _ =>
        Task.FromResult(new ApiResult<PagedResult<ModerationAuditDto>?>(true, HttpStatusCode.OK, new PagedResult<ModerationAuditDto>
        {
            Items = Array.Empty<ModerationAuditDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20
        }));
    public Func<string?, int, Task<ApiResult<IReadOnlyList<ModerationUserLookupDto>?>>> SearchUsersHandler { get; set; } = (_, _) =>
        Task.FromResult(new ApiResult<IReadOnlyList<ModerationUserLookupDto>?>(true, HttpStatusCode.OK, Array.Empty<ModerationUserLookupDto>()));
    public Func<int, Task<ApiResult<ModerationPostPreviewDto?>>> GetPostPreviewHandler { get; set; } = _ =>
        Task.FromResult(new ApiResult<ModerationPostPreviewDto?>(true, HttpStatusCode.OK, null));
    public Func<int, Task<ApiResult<ModerationCommentPreviewDto?>>> GetCommentPreviewHandler { get; set; } = _ =>
        Task.FromResult(new ApiResult<ModerationCommentPreviewDto?>(true, HttpStatusCode.OK, null));

    public Task<bool> HidePostAsync(int postId, ModerationActionRequestDto request) => HidePostHandler(postId, request);
    public Task<bool> RestorePostAsync(int postId, ModerationActionRequestDto request) => RestorePostHandler(postId, request);
    public Task<bool> HideCommentAsync(int commentId, ModerationActionRequestDto request) => HideCommentHandler(commentId, request);
    public Task<bool> RestoreCommentAsync(int commentId, ModerationActionRequestDto request) => RestoreCommentHandler(commentId, request);
    public Task<bool> SuspendUserAsync(string userId, SuspendUserRequestDto request) => SuspendUserHandler(userId, request);
    public Task<bool> UnsuspendUserAsync(string userId, ModerationActionRequestDto request) => UnsuspendUserHandler(userId, request);
    public Task<ApiResult<PagedResult<ModerationAuditDto>?>> GetAuditAsync(ModerationAuditQueryDto query) => GetAuditHandler(query);
    public Task<ApiResult<IReadOnlyList<ModerationUserLookupDto>?>> SearchUsersAsync(string? query, int limit = 10) => SearchUsersHandler(query, limit);
    public Task<ApiResult<ModerationPostPreviewDto?>> GetPostPreviewAsync(int postId) => GetPostPreviewHandler(postId);
    public Task<ApiResult<ModerationCommentPreviewDto?>> GetCommentPreviewAsync(int commentId) => GetCommentPreviewHandler(commentId);
}

internal sealed class StubSearchService : ISearchService
{
    public Func<string?, Task<ApiResult<SearchOverviewDto?>>> GetOverviewHandler { get; set; } = _ =>
        Task.FromResult(new ApiResult<SearchOverviewDto?>(true, HttpStatusCode.OK, new SearchOverviewDto()));
    public Func<string?, string?, int, int, Task<ApiResult<PagedResult<PostSummaryDto>?>>> SearchPostsHandler { get; set; } = (_, _, page, pageSize) =>
        Task.FromResult(new ApiResult<PagedResult<PostSummaryDto>?>(true, HttpStatusCode.OK, new PagedResult<PostSummaryDto>
        {
            Items = Array.Empty<PostSummaryDto>(),
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = 0
        }));
    public Func<string?, int, int, Task<ApiResult<PagedResult<UserListItemDto>?>>> SearchUsersHandler { get; set; } = (_, page, pageSize) =>
        Task.FromResult(new ApiResult<PagedResult<UserListItemDto>?>(true, HttpStatusCode.OK, new PagedResult<UserListItemDto>
        {
            Items = Array.Empty<UserListItemDto>(),
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = 0
        }));
    public Func<string?, int, int, Task<ApiResult<PagedResult<SearchTagResultDto>?>>> SearchTagsHandler { get; set; } = (_, page, pageSize) =>
        Task.FromResult(new ApiResult<PagedResult<SearchTagResultDto>?>(true, HttpStatusCode.OK, new PagedResult<SearchTagResultDto>
        {
            Items = Array.Empty<SearchTagResultDto>(),
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = 0
        }));

    public Task<ApiResult<SearchOverviewDto?>> GetOverviewAsync(string? query) => GetOverviewHandler(query);
    public Task<ApiResult<PagedResult<PostSummaryDto>?>> SearchPostsAsync(string? query, string? tag, int page, int pageSize) => SearchPostsHandler(query, tag, page, pageSize);
    public Task<ApiResult<PagedResult<UserListItemDto>?>> SearchUsersAsync(string? query, int page, int pageSize) => SearchUsersHandler(query, page, pageSize);
    public Task<ApiResult<PagedResult<SearchTagResultDto>?>> SearchTagsAsync(string? query, int page, int pageSize) => SearchTagsHandler(query, page, pageSize);
}

internal sealed class StubReportService : IReportService
{
    public Func<int, CreateReportRequestDto, Task<bool>> ReportPostHandler { get; set; } = (_, _) => Task.FromResult(true);
    public Func<int, CreateReportRequestDto, Task<bool>> ReportCommentHandler { get; set; } = (_, _) => Task.FromResult(true);
    public Func<string, CreateReportRequestDto, Task<bool>> ReportUserHandler { get; set; } = (_, _) => Task.FromResult(true);
    public Func<ReportQueueQueryDto, Task<ApiResult<PagedResult<ReportQueueItemDto>?>>> GetQueueHandler { get; set; } = query =>
        Task.FromResult(new ApiResult<PagedResult<ReportQueueItemDto>?>(true, HttpStatusCode.OK, new PagedResult<ReportQueueItemDto>
        {
            Items = Array.Empty<ReportQueueItemDto>(),
            PageNumber = query.Page,
            PageSize = query.PageSize,
            TotalCount = 0
        }));
    public Func<long, ResolveReportRequestDto, Task<bool>> ResolveHandler { get; set; } = (_, _) => Task.FromResult(true);

    public Task<bool> ReportPostAsync(int postId, CreateReportRequestDto request) => ReportPostHandler(postId, request);
    public Task<bool> ReportCommentAsync(int commentId, CreateReportRequestDto request) => ReportCommentHandler(commentId, request);
    public Task<bool> ReportUserAsync(string userId, CreateReportRequestDto request) => ReportUserHandler(userId, request);
    public Task<ApiResult<PagedResult<ReportQueueItemDto>?>> GetQueueAsync(ReportQueueQueryDto query) => GetQueueHandler(query);
    public Task<bool> ResolveAsync(long reportId, ResolveReportRequestDto request) => ResolveHandler(reportId, request);
}

using System;
using System.Collections.Generic;
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
    public Func<string, Task<UserProfileDto>> GetByIdHandler { get; set; } = id => Task.FromResult(new UserProfileDto
    {
        UserId = id,
        DisplayName = $"User {id}"
    });

    public Task<UserProfileDto?> GetMineAsync() => GetMineHandler();
    public Task<UserProfileDto> CreateMineAsync(CreateUserProfileDto dto) => CreateMineHandler(dto);
    public Task<UserProfileDto> UpdateMineAsync(UpdateUserProfileDto dto) => UpdateMineHandler(dto);
    public Task<UserProfileDto> UploadAvatarAsync(IBrowserFile file, long maxSizeBytes = 5_000_000) => UploadAvatarHandler(file, maxSizeBytes);
    public Task<UserProfileDto> RemoveAvatarAsync() => RemoveAvatarHandler();
    public Task<UserProfileDto> GetUserProfileById(string id) => GetByIdHandler(id);
}

internal sealed class StubPostService : IPostService
{
    public Func<int, int, Task<ApiResult<PagedResult<PostSummaryDto>>>> GetAllHandler { get; set; } = (_, _) =>
        Task.FromResult(new ApiResult<PagedResult<PostSummaryDto>>(true, HttpStatusCode.OK, new PagedResult<PostSummaryDto>()));

    public Func<int, int, bool, bool, Task<ApiResult<PagedResult<PostSummaryDto>>>> GetAllWithVisibilityHandler { get; set; } = (_, _, _, _) =>
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
    public Task<IEnumerable<PostSummaryDto>> GetTopPosts(int count, TimeSpan timeOffcet) => GetTopPostsHandler(count, timeOffcet);
    public Task<PostDto> GetByIdAsync(int id) => GetByIdHandler(id);
    public Task<PostDto> CreateAsync(CreatePostDto dto) => CreateHandler(dto);
    public Task<PostDto> CreateWithImageAsync(MultipartFormDataContent content) => CreateWithImageHandler(content);
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

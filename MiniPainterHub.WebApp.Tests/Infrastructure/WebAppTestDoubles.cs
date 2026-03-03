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

    public Func<int, Task<PostDto>> GetByIdHandler { get; set; } = id =>
        Task.FromResult(new PostDto { Id = id, Title = $"Post {id}", Content = "Stub content", CreatedById = "stub-user" });

    public Func<CreatePostDto, Task<PostDto>> CreateHandler { get; set; } = dto =>
        Task.FromResult(new PostDto { Id = 42, Title = dto.Title, Content = dto.Content, CreatedById = "stub-user" });

    public Func<MultipartFormDataContent, Task<PostDto>> CreateWithImageHandler { get; set; } = _ =>
        Task.FromResult(new PostDto { Id = 43, Title = "Uploaded", Content = "Uploaded content", CreatedById = "stub-user" });

    public Func<int, TimeSpan, Task<IEnumerable<PostSummaryDto>>> GetTopPostsHandler { get; set; } = (_, _) =>
        Task.FromResult<IEnumerable<PostSummaryDto>>(Array.Empty<PostSummaryDto>());

    public Task<ApiResult<PagedResult<PostSummaryDto>>> GetAllAsync(int page, int pageSize) => GetAllHandler(page, pageSize);
    public Task<IEnumerable<PostSummaryDto>> GetTopPosts(int count, TimeSpan timeOffcet) => GetTopPostsHandler(count, timeOffcet);
    public Task<PostDto> GetByIdAsync(int id) => GetByIdHandler(id);
    public Task<PostDto> CreateAsync(CreatePostDto dto) => CreateHandler(dto);
    public Task<PostDto> CreateWithImageAsync(MultipartFormDataContent content) => CreateWithImageHandler(content);
}

internal sealed class StubCommentService : ICommentService
{
    public Func<int, int, int, Task<ApiResult<PagedResult<CommentDto>>>> GetByPostHandler { get; set; } = (_, _, _) =>
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

    public Task<ApiResult<PagedResult<CommentDto>>> GetByPostAsync(int postId, int page, int pageSize) => GetByPostHandler(postId, page, pageSize);
    public Task<ApiResult<CommentDto?>> CreateAsync(int postId, CreateCommentDto dto) => CreateHandler(postId, dto);
}

internal sealed class StubLikeService : ILikeService
{
    public Func<int, Task<LikeDto>> GetLikesHandler { get; set; } = _ => Task.FromResult(new LikeDto { Count = 0, UserHasLiked = false });
    public Func<int, Task<LikeDto>> ToggleLikeHandler { get; set; } = _ => Task.FromResult(new LikeDto { Count = 1, UserHasLiked = true });

    public Task<LikeDto> GetLikesAsync(int postId) => GetLikesHandler(postId);
    public Task<LikeDto> ToggleLikeAsync(int postId) => ToggleLikeHandler(postId);
}

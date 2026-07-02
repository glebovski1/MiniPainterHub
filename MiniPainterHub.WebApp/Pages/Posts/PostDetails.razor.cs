using Microsoft.AspNetCore.Components;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Shared.Viewer;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Pages.Posts;

/// <summary>
/// Coordinates the post details page, including post metadata, comments, moderation actions, and rich image viewer state.
/// </summary>
public partial class PostDetails
{
    /// <summary>
    /// Route-supplied post identifier used to load both the public post details and viewer-specific image data.
    /// </summary>
    [Parameter] public int PostId { get; set; }

    private CommentList? _commentList;
    private CommentList? _viewerCommentList;
    private PostDto? post;
    private PostViewerDto? viewer;
    private bool isLoading;
    private string? loadError;
    private HttpStatusCode? loadStatus;
    private bool _postModerationBusy;
    private string? _postModerationError;
    private string? _postModerationResult;
    private bool _isViewerOpen;
    private ViewerSideTab _viewerSideTab = ViewerSideTab.Info;
    private int _activeImageId;
    private bool _isCommentPlacementMode;
    private ViewerMarkDraftDto? _draftCommentMark;
    private int? _activeCommentId;
    private CommentMarkDto? _activeCommentMark;
    private int? _markLoadingCommentId;
    private int? _markErrorCommentId;
    private string? _markError;

    // Increments every time a comment-marker request is superseded so stale async responses cannot overwrite current state.
    private int _commentMarkRequestVersion;

    // Tracks when the page-level comment list must be refreshed after the viewer-owned list handled the immediate update.
    private bool _pageCommentsRefreshPending;

    // Caches loaded marker coordinates by comment id to avoid refetching marker data while the viewer stays on the page.
    private readonly Dictionary<int, CommentMarkDto> _commentMarkCache = new();

    private string ViewerInfoTabId => $"viewer-side-tab-info-{PostId}";
    private string ViewerCommentsTabId => $"viewer-side-tab-comments-{PostId}";
    private string ViewerInfoPanelId => $"viewer-side-panel-info-{PostId}";
    private string ViewerCommentsPanelId => $"viewer-side-panel-comments-{PostId}";

    private PostViewerImageDto? CurrentPreviewImage =>
        viewer?.Images.FirstOrDefault(image => image.Id == ResolvedActiveImageId) ?? viewer?.Images.FirstOrDefault();

    private int ResolvedActiveImageId =>
        viewer?.Images.Any(image => image.Id == _activeImageId) == true
            ? _activeImageId
            : viewer?.Images.FirstOrDefault()?.Id ?? 0;

    private ViewerSideTab PreferredViewerSideTab =>
        _isCommentPlacementMode || _draftCommentMark is not null || _activeCommentId.HasValue
            ? ViewerSideTab.Comments
            : ViewerSideTab.Info;

    private bool ShouldRenderViewerComments =>
        _viewerSideTab == ViewerSideTab.Comments
        || _isCommentPlacementMode
        || _draftCommentMark is not null
        || _activeCommentId.HasValue;

    private bool HasPaintRecipe =>
        post is not null
        && (!string.IsNullOrWhiteSpace(post.MiniatureName)
            || !string.IsNullOrWhiteSpace(post.PaintsUsed)
            || !string.IsNullOrWhiteSpace(post.Techniques)
            || !string.IsNullOrWhiteSpace(post.Difficulty)
            || !string.IsNullOrWhiteSpace(post.TimeSpent));

    private IEnumerable<RecipeDetailItem> PaintRecipeItems
    {
        get
        {
            if (post is null)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(post.MiniatureName))
            {
                yield return new RecipeDetailItem("Miniature", post.MiniatureName);
            }

            if (!string.IsNullOrWhiteSpace(post.PaintsUsed))
            {
                yield return new RecipeDetailItem("Paints", post.PaintsUsed);
            }

            if (!string.IsNullOrWhiteSpace(post.Techniques))
            {
                yield return new RecipeDetailItem("Techniques", post.Techniques);
            }

            if (!string.IsNullOrWhiteSpace(post.Difficulty))
            {
                yield return new RecipeDetailItem("Difficulty", post.Difficulty);
            }

            if (!string.IsNullOrWhiteSpace(post.TimeSpent))
            {
                yield return new RecipeDetailItem("Time", post.TimeSpent);
            }
        }
    }

    private bool IsViewerCommentsSurfaceActive =>
        _isViewerOpen
        && _viewerSideTab == ViewerSideTab.Comments
        && ShouldRenderViewerComments;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await LoadPostAsync();
    }

    /// <summary>
    /// Loads the post details and viewer payload in parallel so the page and rich viewer stay synchronized.
    /// </summary>
    private async Task LoadPostAsync()
    {
        isLoading = true;
        loadError = null;
        loadStatus = null;

        var postRequest = new HttpRequestMessage(HttpMethod.Get, $"api/posts/{PostId}");
        var viewerRequest = new HttpRequestMessage(HttpMethod.Get, $"api/posts/{PostId}/viewer");

        try
        {
            var postTask = Api.SendForResultAsync<PostDto>(postRequest, new ApiRequestOptions { SuppressErrorNotifications = true });
            var viewerTask = Api.SendForResultAsync<PostViewerDto>(viewerRequest, new ApiRequestOptions { SuppressErrorNotifications = true });

            await Task.WhenAll(postTask, viewerTask);

            var postResponse = await postTask;
            var viewerResponse = await viewerTask;

            if (postResponse.Success && postResponse.Value is not null && viewerResponse.Success && viewerResponse.Value is not null)
            {
                post = postResponse.Value;
                viewer = viewerResponse.Value;
                _activeImageId = viewer.Images.FirstOrDefault()?.Id ?? 0;
                _isViewerOpen = false;
                ResetCommentViewerState();
            }
            else
            {
                post = null;
                viewer = null;
                loadStatus = viewerResponse.StatusCode ?? postResponse.StatusCode;
                loadError = loadStatus == HttpStatusCode.NotFound
                    ? "This post could not be found."
                    : "We couldn't load this post. Please check your connection and try again.";
            }
        }
        catch
        {
            post = null;
            viewer = null;
            loadStatus = null;
            loadError = "We couldn't load this post. Please try again in a moment.";
        }
        finally
        {
            isLoading = false;
        }
    }

    /// <summary>
    /// Refreshes the active comment surface while preserving the viewer/page split between duplicate comment lists.
    /// </summary>
    private async Task ReloadCommentsAsync()
    {
        if (IsViewerCommentsSurfaceActive && _viewerCommentList is not null)
        {
            await _viewerCommentList.ReloadAsync();
            _pageCommentsRefreshPending = _commentList is not null;
            return;
        }

        if (_commentList is not null)
        {
            await _commentList.ReloadAsync();
            _pageCommentsRefreshPending = false;
        }
    }

    private void GoToLogin()
    {
        var returnUrl = Uri.EscapeDataString(Nav.Uri);
        Nav.NavigateTo($"/login?returnUrl={returnUrl}", true);
    }

    private Task HidePostAsync() => ModeratePostAsync(hide: true);

    private Task RestorePostAsync() => ModeratePostAsync(hide: false);

    /// <summary>
    /// Runs the inline moderation action and reloads page state so hidden/restored status is reflected immediately.
    /// </summary>
    private async Task ModeratePostAsync(bool hide)
    {
        _postModerationError = null;
        _postModerationResult = null;
        _postModerationBusy = true;

        try
        {
            var request = new ModerationActionRequestDto
            {
                Reason = "Inline moderation from post details."
            };

            var success = hide
                ? await ModerationService.HidePostAsync(PostId, request)
                : await ModerationService.RestorePostAsync(PostId, request);

            if (!success)
            {
                _postModerationError = "Moderation action failed.";
                return;
            }

            _postModerationResult = hide ? "Post hidden." : "Post restored.";
            await LoadPostAsync();
            await ReloadCommentsAsync();
        }
        finally
        {
            _postModerationBusy = false;
        }
    }

    private async Task HandleActiveImageChangedAsync(int imageId)
    {
        _activeImageId = imageId;

        if (_activeCommentId.HasValue && (_activeCommentMark is null || _activeCommentMark.PostImageId != imageId))
        {
            await ClearActiveCommentMarkAsync();
        }
    }

    private Task OpenViewerAsync()
    {
        if (viewer is null || viewer.Images.Count == 0)
        {
            return Task.CompletedTask;
        }

        _viewerSideTab = PreferredViewerSideTab;
        _activeImageId = ResolvedActiveImageId == 0 ? viewer.Images[0].Id : ResolvedActiveImageId;
        _isViewerOpen = true;
        return Task.CompletedTask;
    }

    private async Task OpenViewerAtImageAsync(int imageId)
    {
        _viewerSideTab = PreferredViewerSideTab;
        _isViewerOpen = true;
        await HandleActiveImageChangedAsync(imageId);
    }

    private async Task CloseViewerAsync()
    {
        _isViewerOpen = false;
        _viewerSideTab = ViewerSideTab.Info;
        await ClearActiveCommentMarkAsync();
        _markLoadingCommentId = null;
        _markErrorCommentId = null;
        _markError = null;

        if (_isCommentPlacementMode)
        {
            _isCommentPlacementMode = false;
        }

        if (_pageCommentsRefreshPending && _commentList is not null)
        {
            _pageCommentsRefreshPending = false;
            await _commentList.ReloadAsync();
        }
    }

    private Task BeginCommentPlacementAsync()
    {
        if (viewer is null || viewer.Images.Count == 0)
        {
            return Task.CompletedTask;
        }

        _viewerSideTab = ViewerSideTab.Comments;
        _isViewerOpen = true;
        _activeImageId = _draftCommentMark?.PostImageId ?? ResolvedActiveImageId;
        if (_activeImageId == 0)
        {
            _activeImageId = viewer.Images[0].Id;
        }

        _isCommentPlacementMode = true;
        _activeCommentMark = null;
        _activeCommentId = null;
        _markLoadingCommentId = null;
        _markErrorCommentId = null;
        _markError = null;
        _commentMarkRequestVersion++;
        return Task.CompletedTask;
    }

    private Task HandleCommentPlacementSelectedAsync(ViewerMarkDraftDto draft)
    {
        _viewerSideTab = ViewerSideTab.Comments;
        _draftCommentMark = draft;
        _activeImageId = draft.PostImageId;
        _isCommentPlacementMode = false;
        return Task.CompletedTask;
    }

    private Task CancelCommentPlacementAsync()
    {
        _isCommentPlacementMode = false;
        return Task.CompletedTask;
    }

    private Task RemoveDraftCommentMarkAsync()
    {
        _draftCommentMark = null;
        _isCommentPlacementMode = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Promotes a newly created marked comment into the active viewer marker without waiting for another fetch.
    /// </summary>
    private async Task HandleCommentAddedAsync(CommentDto createdComment)
    {
        _viewerSideTab = ViewerSideTab.Comments;

        if (_draftCommentMark is not null && createdComment.HasViewerMark)
        {
            var mark = new CommentMarkDto
            {
                CommentId = createdComment.Id,
                PostImageId = createdComment.MarkedPostImageId ?? _draftCommentMark.PostImageId,
                NormalizedX = _draftCommentMark.NormalizedX,
                NormalizedY = _draftCommentMark.NormalizedY
            };

            _commentMarkCache[createdComment.Id] = mark;
            _activeCommentId = createdComment.Id;
            _activeCommentMark = mark;
            _activeImageId = mark.PostImageId;
        }

        _draftCommentMark = null;
        _isCommentPlacementMode = false;
        _markError = null;
        _markErrorCommentId = null;
        _markLoadingCommentId = null;
        await ReloadCommentsAsync();
    }

    /// <summary>
    /// Toggles comment-marker focus and ignores stale marker loads when the user selects another comment first.
    /// </summary>
    private async Task ToggleCommentMarkAsync(CommentDto comment)
    {
        _viewerSideTab = ViewerSideTab.Comments;

        if (_activeCommentId == comment.Id)
        {
            await ClearActiveCommentMarkAsync();
            return;
        }

        _commentMarkRequestVersion++;
        var requestVersion = _commentMarkRequestVersion;

        if (comment.MarkedPostImageId.HasValue)
        {
            _activeImageId = comment.MarkedPostImageId.Value;
        }

        _isViewerOpen = true;
        _activeCommentId = comment.Id;
        _activeCommentMark = null;
        _markErrorCommentId = null;
        _markError = null;
        _markLoadingCommentId = comment.Id;
        _isCommentPlacementMode = false;

        if (!_commentMarkCache.TryGetValue(comment.Id, out var mark))
        {
            try
            {
                mark = await CommentMarkService.GetByCommentIdAsync(comment.Id, comment.IsDeleted);
            }
            catch
            {
                if (requestVersion != _commentMarkRequestVersion || _activeCommentId != comment.Id)
                {
                    return;
                }

                _markLoadingCommentId = null;
                _markErrorCommentId = comment.Id;
                _markError = "We couldn't show this marker right now.";
                return;
            }

            if (requestVersion != _commentMarkRequestVersion || _activeCommentId != comment.Id)
            {
                return;
            }

            _commentMarkCache[comment.Id] = mark;
        }

        _markLoadingCommentId = null;
        _markErrorCommentId = null;
        _markError = null;
        _activeCommentMark = mark;
        _activeImageId = mark.PostImageId;
    }

    /// <summary>
    /// Clears the active comment marker and invalidates any in-flight marker request.
    /// </summary>
    private Task ClearActiveCommentMarkAsync()
    {
        _commentMarkRequestVersion++;
        _activeCommentId = null;
        _activeCommentMark = null;
        _markLoadingCommentId = null;
        _markErrorCommentId = null;
        _markError = null;
        return Task.CompletedTask;
    }

    private int? GetImageNumber(int? postImageId)
    {
        if (viewer is null || !postImageId.HasValue)
        {
            return null;
        }

        var index = viewer.Images.FindIndex(image => image.Id == postImageId.Value);
        return index >= 0 ? index + 1 : null;
    }

    /// <summary>
    /// Resets all viewer/comment marker state when the page loads a different post or reloads the current post.
    /// </summary>
    private void ResetCommentViewerState()
    {
        _commentMarkCache.Clear();
        _isViewerOpen = false;
        _viewerSideTab = ViewerSideTab.Info;
        _activeCommentId = null;
        _activeCommentMark = null;
        _draftCommentMark = null;
        _isCommentPlacementMode = false;
        _markLoadingCommentId = null;
        _markErrorCommentId = null;
        _markError = null;
        _commentMarkRequestVersion = 0;
        _pageCommentsRefreshPending = false;
    }

    private static string GetThumbnailSource(PostViewerImageDto image) =>
        image.ThumbnailUrl
        ?? image.PreviewUrl
        ?? ReplaceVariantSuffix(image.ImageUrl, "_thumb.");

    private static string GetPreviewSource(PostViewerImageDto image) =>
        image.PreviewUrl
        ?? ReplaceVariantSuffix(image.ImageUrl, "_preview.");

    private static string ReplaceVariantSuffix(string? url, string replacement)
    {
        if (string.IsNullOrEmpty(url))
        {
            return string.Empty;
        }

        return url.Contains("_max.", StringComparison.OrdinalIgnoreCase)
            ? url.Replace("_max.", replacement, StringComparison.OrdinalIgnoreCase)
            : url;
    }

    private void SetViewerSideTab(ViewerSideTab tab)
    {
        _viewerSideTab = tab;
    }

    private enum ViewerSideTab
    {
        Info,
        Comments
    }

    private sealed record RecipeDetailItem(string Label, string Value);
}

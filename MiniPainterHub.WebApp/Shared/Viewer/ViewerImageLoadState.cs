using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.WebApp.Shared.Viewer;

internal sealed class ViewerImageLoadState
{
    private readonly Dictionary<int, int> _retryTokens = new();
    private readonly HashSet<int> _loadedImageIds = new();
    private readonly HashSet<int> _failedImageIds = new();

    public bool IsLoading(PostViewerImageDto? image) =>
        image is not null && !_loadedImageIds.Contains(image.Id) && !_failedImageIds.Contains(image.Id);

    public bool IsReady(PostViewerImageDto? image) =>
        image is not null && _loadedImageIds.Contains(image.Id) && !_failedImageIds.Contains(image.Id);

    public bool IsFailed(PostViewerImageDto? image) =>
        image is not null && _failedImageIds.Contains(image.Id);

    public void Clear()
    {
        _retryTokens.Clear();
        _loadedImageIds.Clear();
        _failedImageIds.Clear();
    }

    public void MarkLoaded(int imageId)
    {
        _failedImageIds.Remove(imageId);
        _loadedImageIds.Add(imageId);
    }

    public void MarkFailed(int imageId)
    {
        _loadedImageIds.Remove(imageId);
        _failedImageIds.Add(imageId);
    }

    public void Retry(int imageId)
    {
        _failedImageIds.Remove(imageId);
        _loadedImageIds.Remove(imageId);
        _retryTokens[imageId] = _retryTokens.TryGetValue(imageId, out var current) ? current + 1 : 1;
    }

    public string GetCurrentSource(PostViewerImageDto? image, ViewerScaleMode scaleMode) =>
        image is null
            ? string.Empty
            : AppendRetryToken(GetInteractiveSource(image, scaleMode), image.Id);

    public static string GetInteractiveSource(PostViewerImageDto image, ViewerScaleMode scaleMode) =>
        scaleMode == ViewerScaleMode.ActualSize
            ? image.ImageUrl
            : image.PreviewUrl ?? image.ImageUrl;

    public static string GetPreloadSource(PostViewerImageDto image) =>
        image.PreviewUrl ?? image.ImageUrl;

    public static string[] GetAdjacentPreloadSources(IReadOnlyList<PostViewerImageDto> images, int currentImageId)
    {
        if (images.Count < 2)
        {
            return Array.Empty<string>();
        }

        var currentIndex = -1;
        for (var i = 0; i < images.Count; i++)
        {
            if (images[i].Id == currentImageId)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return Array.Empty<string>();
        }

        var previousIndex = currentIndex == 0 ? images.Count - 1 : currentIndex - 1;
        var nextIndex = currentIndex >= images.Count - 1 ? 0 : currentIndex + 1;
        var nextNextIndex = nextIndex >= images.Count - 1 ? 0 : nextIndex + 1;

        return new[]
            {
                GetPreloadSource(images[previousIndex]),
                GetPreloadSource(images[nextIndex]),
                GetPreloadSource(images[nextNextIndex])
            }
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private string AppendRetryToken(string url, int imageId)
    {
        if (!_retryTokens.TryGetValue(imageId, out var token) || token <= 0)
        {
            return url;
        }

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}retry={token}";
    }
}

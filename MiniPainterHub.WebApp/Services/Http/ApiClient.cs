using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MiniPainterHub.WebApp.Services.Notifications;

namespace MiniPainterHub.WebApp.Services.Http;

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly INotificationService _notifications;

    public ApiClient(HttpClient httpClient, INotificationService notifications)
    {
        _httpClient = httpClient;
        _notifications = notifications;
    }

    public async Task<T?> SendAsync<T>(HttpRequestMessage request, ApiRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
            {
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        }

        await HandleErrorAsync(response, options, cancellationToken);
        return default;
    }

    public async Task<bool> SendAsync(HttpRequestMessage request, ApiRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        await HandleErrorAsync(response, options, cancellationToken);
        return false;
    }

    private async Task HandleErrorAsync(HttpResponseMessage response, ApiRequestOptions? options, CancellationToken cancellationToken)
    {
        var body = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken);
        ProblemDetailsPayload? problem = null;

        if (!string.IsNullOrWhiteSpace(body) && response.Content?.Headers?.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                problem = JsonSerializer.Deserialize<ProblemDetailsPayload>(body, SerializerOptions);
            }
            catch
            {
                // ignored
            }
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errors = ExtractErrors(problem, body);
            if (errors?.Count > 0)
            {
                if (options?.SuppressErrorNotifications != true)
                {
                    await _notifications.ShowValidationErrorsAsync(errors);
                }
                return;
            }
        }

        if (options?.SuppressErrorNotifications == true)
        {
            return;
        }

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                await _notifications.ShowInfoAsync("Please sign in to continue.", "Authentication required");
                break;
            default:
                var title = string.IsNullOrWhiteSpace(problem?.Title) ? "Request failed" : problem.Title!;
                var detail = !string.IsNullOrWhiteSpace(problem?.Detail)
                    ? problem!.Detail!
                    : problem?.Status is int statusValue
                        ? $"Request failed ({statusValue})."
                        : $"Request failed ({(int)response.StatusCode}).";
                await _notifications.ShowErrorAsync(detail, title);
                break;
        }
    }

    private static IDictionary<string, string[]>? ExtractErrors(ProblemDetailsPayload? problem, string? raw)
    {
        if (problem?.Errors is { Count: > 0 } direct)
        {
            return direct;
        }

        if (problem?.Extensions != null && problem.Extensions.TryGetValue("errors", out var extensionsElement))
        {
            var dict = ConvertToDictionary(extensionsElement);
            if (dict is { Count: > 0 })
            {
                return dict;
            }
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var document = JsonDocument.Parse(raw);
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("errors", out var rootErrors))
                {
                    return ConvertToDictionary(rootErrors);
                }
            }
            catch
            {
                // ignored
            }
        }

        return null;
    }

    private static IDictionary<string, string[]>? ConvertToDictionary(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                var values = property.Value
                    .EnumerateArray()
                    .Select(v => v.GetString())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!)
                    .ToArray();

                if (values.Length > 0)
                {
                    result[property.Name] = values;
                }
            }
            else if (property.Value.ValueKind == JsonValueKind.String)
            {
                var value = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result[property.Name] = new[] { value! };
                }
            }
        }

        return result.Count == 0 ? null : result;
    }

    private sealed class ProblemDetailsPayload
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public int? Status { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extensions { get; set; }
    }
}

public sealed class ApiRequestOptions
{
    public bool SuppressErrorNotifications { get; init; }
}

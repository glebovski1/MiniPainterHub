using NBomber.CSharp;
using NBomber.Contracts;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var settings = LoadSettings.Parse(args);
using var setupClient = new HttpClient { BaseAddress = settings.BaseUri };

await WaitForReadinessAsync(setupClient, settings);

var token = await LoginAsync(setupClient, settings.UserName, settings.Password);
var postId = await ResolvePostIdAsync(setupClient);

Console.WriteLine($"MiniPainterHub load profile: {settings.Profile}");
Console.WriteLine($"Base URL: {settings.BaseUri}");
Console.WriteLine($"Seed post ID: {postId}");

var sharedClient = new HttpClient { BaseAddress = settings.BaseUri };
if (!string.IsNullOrWhiteSpace(token))
{
    sharedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}

var uploadImage = Convert.FromBase64String(
    "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////2wBDAf//////////////////////////////////////////////////////////////////////////////////////wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAX/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIQAxAAAAH/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAEFAqf/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oACAEDAQE/ASP/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oACAECAQE/ASP/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAY/Aqf/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAE/ISP/2gAMAwEAAgADAAAAEP/EFBQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQMBAT8QH//EFBQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQIBAT8QH//EFBABAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAT8QH//Z");

var scenarios = new[]
{
    Scenario.Create("public_feed", async _ =>
        await SendAsync(sharedClient, () => new HttpRequestMessage(HttpMethod.Get, "/api/posts?page=1&pageSize=20"))),

    Scenario.Create("post_details", async _ =>
        await SendAsync(sharedClient, () => new HttpRequestMessage(HttpMethod.Get, $"/api/posts/{postId}"))),

    Scenario.Create("viewer_payload", async _ =>
        await SendAsync(sharedClient, () => new HttpRequestMessage(HttpMethod.Get, $"/api/posts/{postId}/viewer"))),

    Scenario.Create("search_posts", async _ =>
        await SendAsync(sharedClient, () => new HttpRequestMessage(HttpMethod.Get, "/api/search/posts?q=glazing&page=1&pageSize=10"))),

    Scenario.Create("login", async _ =>
        await SendAsync(sharedClient, () => JsonRequest(HttpMethod.Post, "/api/auth/login", new
        {
            userName = settings.UserName,
            password = settings.Password
        }), HttpStatusCode.TooManyRequests)),

    Scenario.Create("comment", async context =>
        await SendAsync(sharedClient, () => JsonRequest(HttpMethod.Post, $"/api/posts/{postId}/comments", new
        {
            postId,
            text = $"Load comment {context.InvocationNumber}"
        }), HttpStatusCode.TooManyRequests)),

    Scenario.Create("like_unlike", async _ =>
    {
        var liked = await SendOkAsync(sharedClient, () => new HttpRequestMessage(HttpMethod.Post, $"/api/posts/{postId}/likes"), HttpStatusCode.TooManyRequests);
        if (!liked)
        {
            return Response.Fail();
        }

        return await SendAsync(sharedClient, () => new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{postId}/likes"), HttpStatusCode.TooManyRequests);
    }),

    Scenario.Create("post_image_upload", async context =>
        await SendAsync(sharedClient, () => BuildPostUploadRequest(uploadImage, context.InvocationNumber), HttpStatusCode.TooManyRequests)),

    Scenario.Create("guide_photo_upload", async context =>
        await SendAsync(sharedClient, () => BuildGuideUploadRequest(uploadImage, context.InvocationNumber), HttpStatusCode.TooManyRequests))
};

var load = settings.Profile.Equals("event", StringComparison.OrdinalIgnoreCase)
    ? LoadProfiles.EventSpike
    : LoadProfiles.Smoke;

NBomberRunner
    .RegisterScenarios(scenarios.Select(scenario => scenario.WithLoadSimulations(load)).ToArray())
    .WithTestSuite("MiniPainterHub")
    .WithTestName($"{settings.Profile}-load")
    .WithReportFolder(Path.Combine("artifacts", "load-tests"))
    .Run();

static async Task<IResponse> SendAsync(HttpClient client, Func<HttpRequestMessage> requestFactory, params HttpStatusCode[] additionalOkStatuses)
{
    return await SendOkAsync(client, requestFactory, additionalOkStatuses)
        ? Response.Ok()
        : Response.Fail();
}

static async Task<bool> SendOkAsync(HttpClient client, Func<HttpRequestMessage> requestFactory, params HttpStatusCode[] additionalOkStatuses)
{
    using var request = requestFactory();
    using var response = await client.SendAsync(request);
    return response.IsSuccessStatusCode || additionalOkStatuses.Contains(response.StatusCode);
}

static HttpRequestMessage JsonRequest(HttpMethod method, string path, object body) =>
    new(method, path)
    {
        Content = JsonContent.Create(body)
    };

static HttpRequestMessage BuildPostUploadRequest(byte[] uploadImage, long invocationNumber)
{
    var content = new MultipartFormDataContent
    {
        { new StringContent($"Load post {DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{invocationNumber}"), "Title" },
        { new StringContent("Load-test miniature upload."), "Content" },
        { new StringContent("load-test"), "tags" }
    };

    var image = new ByteArrayContent(uploadImage);
    image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
    content.Add(image, "images", "load-test.jpg");

    return new HttpRequestMessage(HttpMethod.Post, "/api/posts/with-image")
    {
        Content = content
    };
}

static HttpRequestMessage BuildGuideUploadRequest(byte[] uploadImage, long invocationNumber)
{
    var content = new MultipartFormDataContent
    {
        { new StringContent($"Load guide {DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{invocationNumber}"), "Title" },
        { new StringContent("Load-test guide upload."), "Summary" },
        { new StringContent("Primer, brush, paint"), "Materials" },
        { new StringContent("Basecoat"), "Steps[0].Title" },
        { new StringContent("Apply a quick basecoat."), "Steps[0].Description" },
        { new StringContent("0"), "stepPhotoIndices" }
    };

    var image = new ByteArrayContent(uploadImage);
    image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
    content.Add(image, "stepPhotos", "guide-step.jpg");

    return new HttpRequestMessage(HttpMethod.Post, "/api/guides/with-step-photos")
    {
        Content = content
    };
}

static async Task WaitForReadinessAsync(HttpClient client, LoadSettings settings)
{
    for (var attempt = 1; attempt <= settings.ReadinessAttempts; attempt++)
    {
        try
        {
            using var response = await client.GetAsync("/healthz/ready");
            if (response.IsSuccessStatusCode)
            {
                return;
            }
        }
        catch (HttpRequestException)
        {
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    throw new InvalidOperationException($"Base URL {settings.BaseUri} did not become ready.");
}

static async Task<string?> LoginAsync(HttpClient client, string userName, string password)
{
    using var response = await client.PostAsJsonAsync("/api/auth/login", new
    {
        userName,
        password
    });

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"Login bootstrap failed with HTTP {(int)response.StatusCode}. Write scenarios will report failures.");
        return null;
    }

    using var stream = await response.Content.ReadAsStreamAsync();
    using var json = await JsonDocument.ParseAsync(stream);
    return json.RootElement.TryGetProperty("token", out var token)
        ? token.GetString()
        : null;
}

static async Task<int> ResolvePostIdAsync(HttpClient client)
{
    using var response = await client.GetAsync("/api/posts?page=1&pageSize=1");
    response.EnsureSuccessStatusCode();

    using var stream = await response.Content.ReadAsStreamAsync();
    using var json = await JsonDocument.ParseAsync(stream);
    var items = json.RootElement.GetProperty("items");
    if (items.GetArrayLength() == 0)
    {
        throw new InvalidOperationException("Load tests require at least one public post.");
    }

    return items[0].GetProperty("id").GetInt32();
}

internal sealed record LoadSettings(
    Uri BaseUri,
    string Profile,
    string UserName,
    string Password,
    int ReadinessAttempts)
{
    public static LoadSettings Parse(string[] args)
    {
        var values = ParseArgs(args);
        var baseUrl = GetValue(values, "base-url", "MPH_LOAD_BASE_URL", "http://127.0.0.1:5176");
        var profile = GetValue(values, "profile", "MPH_LOAD_PROFILE", "smoke");
        var userName = GetValue(values, "user", "MPH_LOAD_USER", "user");
        var password = GetValue(values, "password", "MPH_LOAD_PASSWORD", "User123!");
        var attemptsText = GetValue(values, "readiness-attempts", "MPH_LOAD_READINESS_ATTEMPTS", "30");

        return new LoadSettings(
            new Uri(baseUrl, UriKind.Absolute),
            profile,
            userName,
            password,
            int.TryParse(attemptsText, out var attempts) ? attempts : 30);
    }

    private static IReadOnlyDictionary<string, string> ParseArgs(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values[key] = args[++i];
            }
        }

        return values;
    }

    private static string GetValue(IReadOnlyDictionary<string, string> values, string key, string environmentName, string defaultValue) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : Environment.GetEnvironmentVariable(environmentName) ?? defaultValue;
}

internal static class LoadProfiles
{
    public static LoadSimulation[] Smoke { get; } =
    {
        Simulation.Inject(rate: 1, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(15))
    };

    public static LoadSimulation[] EventSpike { get; } =
    {
        Simulation.Inject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(3)),
        Simulation.KeepConstant(copies: 50, during: TimeSpan.FromMinutes(3))
    };
}

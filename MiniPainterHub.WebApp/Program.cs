using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MiniPainterHub.WebApp.Services;
using MiniPainterHub.WebApp.Services.Auth;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;
using MiniPainterHub.WebApp.Services.Notifications;
using MiniPainterHub.WebApp.Services.Performance;
using MiniPainterHub.WebApp.Layout;
using System.Globalization;

namespace MiniPainterHub.WebApp;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        //------------------------------------------------------------
        // 1)  Blazor-WASM authorization core  (<AuthorizeView>, etc.)
        //------------------------------------------------------------
        builder.Services.AddAuthorizationCore();

        //------------------------------------------------------------
        // UI state
        //------------------------------------------------------------
        builder.Services.AddScoped<UserPanelState>();

        //------------------------------------------------------------
        // 2)  JWT-based authentication state provider
        //------------------------------------------------------------
        builder.Services
            .AddScoped<ITokenStore, LocalStorageTokenStore>()
            .AddScoped<JwtAuthenticationStateProvider>()
            .AddScoped<AuthenticationStateProvider>(sp =>
                sp.GetRequiredService<JwtAuthenticationStateProvider>());

        //------------------------------------------------------------
        // 3)  Notifications and base API address
        //------------------------------------------------------------
        builder.Services.AddScoped<INotificationService, BootstrapToastNotificationService>();
        var apiBase = new Uri(builder.HostEnvironment.BaseAddress);

        builder.Services.AddSingleton(CreateClientPerformanceOptions(builder));

        builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiBase });

        //------------------------------------------------------------
        // 4)  API services
        //------------------------------------------------------------
        builder.Services.AddScoped<ApiClient>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IPostService, PostService>();
        builder.Services.AddScoped<IPostViewerService, PostViewerService>();
        builder.Services.AddScoped<IPaintingGuideService, PaintingGuideService>();
        builder.Services.AddScoped<INewsAnnouncementService, NewsAnnouncementService>();
        builder.Services.AddScoped<IAuthorMarkService, AuthorMarkService>();
        builder.Services.AddScoped<ICommentMarkService, CommentMarkService>();
        builder.Services.AddScoped<ICommentService, CommentService>();
        builder.Services.AddScoped<ILikeService, LikeService>();
        builder.Services.AddScoped<IProfileService, ProfileService>();
        builder.Services.AddScoped<IModerationService, ModerationService>();
        builder.Services.AddScoped<IAdminService, AdminService>();
        builder.Services.AddScoped<ISearchService, SearchService>();
        builder.Services.AddScoped<IReportService, ReportService>();
        builder.Services.AddScoped<IFollowService, FollowService>();
        builder.Services.AddScoped<IConversationSummaryService, ConversationSummaryService>();
        builder.Services.AddScoped<IConversationService, ConversationService>();
        builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
        builder.Services.AddScoped<IClientPerformanceMetrics, ClientPerformanceMetricsService>();

        //------------------------------------------------------------
        // 5)  Run!
        //------------------------------------------------------------
        await builder.Build().RunAsync();
    }

    private static ClientPerformanceOptions CreateClientPerformanceOptions(WebAssemblyHostBuilder builder)
    {
        var options = new ClientPerformanceOptions
        {
            Enabled = builder.HostEnvironment.IsProduction(),
            SampleRate = 0.1,
            MaxBatchSize = 50
        };

        var section = builder.Configuration.GetSection("ClientPerformance");
        if (bool.TryParse(section["Enabled"], out var enabled))
        {
            options.Enabled = enabled;
        }

        if (double.TryParse(section["SampleRate"], NumberStyles.Float, CultureInfo.InvariantCulture, out var sampleRate))
        {
            options.SampleRate = sampleRate;
        }

        if (int.TryParse(section["MaxBatchSize"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxBatchSize))
        {
            options.MaxBatchSize = maxBatchSize;
        }

        return options;
    }
}

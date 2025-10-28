using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MiniPainterHub.WebApp.Services;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;
using MiniPainterHub.WebApp.Services.Notifications;

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
        // 2)  JWT-based authentication state provider
        //------------------------------------------------------------
        builder.Services
            .AddScoped<JwtAuthenticationStateProvider>()
            .AddScoped<AuthenticationStateProvider>(sp =>
                sp.GetRequiredService<JwtAuthenticationStateProvider>());

        //------------------------------------------------------------
        // 3)  Message handler that adds  Authorization: Bearer <token>
        //------------------------------------------------------------
        builder.Services.AddTransient<JwtAuthorizationMessageHandler>();

        //------------------------------------------------------------
        // 4)  Notifications and base API address
        //------------------------------------------------------------
        builder.Services.AddScoped<INotificationService, BootstrapToastNotificationService>();
        var apiBase = new Uri(builder.HostEnvironment.BaseAddress);

        //------------------------------------------------------------
        // 5)  Typed HTTP clients
        //------------------------------------------------------------
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IPostService, PostService>();
        builder.Services.AddScoped<ICommentService, CommentService>();
        builder.Services.AddScoped<ILikeService, LikeService>();
        builder.Services.AddScoped<IProfileService, ProfileService>();

        builder.Services
            .AddHttpClient<ApiClient>(client => client.BaseAddress = apiBase)
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        //------------------------------------------------------------
        // 6)  Run!
        //------------------------------------------------------------
        await builder.Build().RunAsync();
    }
}

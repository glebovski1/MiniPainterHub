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
        builder.Services
            .AddHttpClient<IAuthService, AuthService>(c => c.BaseAddress = apiBase);

        builder.Services
            .AddHttpClient<IPostService, PostService>(c => c.BaseAddress = apiBase)
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services
            .AddHttpClient<ICommentService, CommentService>(c => c.BaseAddress = apiBase)
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services
            .AddHttpClient<ILikeService, LikeService>(c => c.BaseAddress = apiBase)
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services.AddHttpClient<IProfileService, ProfileService>(client =>
        {
            client.BaseAddress = apiBase;
        })
        .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services
            .AddHttpClient<ApiClient>(client => client.BaseAddress = apiBase)
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        //------------------------------------------------------------
        // 6)  Run!
        //------------------------------------------------------------
        await builder.Build().RunAsync();
    }
}

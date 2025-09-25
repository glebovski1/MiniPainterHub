using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MiniPainterHub.WebApp.Services.Interfaces;
using MiniPainterHub.WebApp.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace MiniPainterHub.WebApp
{
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
            // 4)  Base API address (same origin for dev)
            //------------------------------------------------------------
            var apiBase = new Uri(builder.HostEnvironment.BaseAddress);

            //------------------------------------------------------------
            // 5)  Typed HTTP clients
            //     • AuthService  – NO auth header required for login/register
            //     • Post/Comment/Like services – SEND auth header when token present
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
                client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
            })
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();


            //------------------------------------------------------------
            // 6)  Run!
            //------------------------------------------------------------
            await builder.Build().RunAsync();
        }
    }
}

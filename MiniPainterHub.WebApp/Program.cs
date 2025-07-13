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

            // 1) Register the custom handler
            builder.Services.AddTransient<JwtAuthorizationMessageHandler>();

            // 2) Register the "Api" client once, using your handler
            builder.Services
                .AddHttpClient("Api", client =>
                {
                    client.BaseAddress = new Uri("https://localhost:7295");
                })
                .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

            // 3) Make "Api" the default HttpClient for injection
            builder.Services.AddScoped(sp =>
                sp.GetRequiredService<IHttpClientFactory>()
                  .CreateClient("Api"));

            // 4) Auth wiring
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();
            builder.Services.AddAuthorizationCore();

            // 5) Your application services
            builder.Services.AddScoped<IPostService, PostService>();

            await builder.Build().RunAsync();
        }
    }
}

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MiniPainterHub.WebApp.Services.Interfaces;
using MiniPainterHub.WebApp.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace MiniPainterHub.WebApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");


            builder.Services.AddHttpClient("Api", client =>
            {
                client.BaseAddress = new Uri("https://localhost:7295");
            });

            builder.Services.AddScoped(sp =>
                sp.GetRequiredService<IHttpClientFactory>()
                  .CreateClient("Api"));
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();
            builder.Services.AddAuthorizationCore();

            await builder.Build().RunAsync();
        }
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MiniPainterHub;

public partial class Program
{
    internal const string LighthouseEnvironmentName = "Lighthouse";
    internal const int SqlServerMaxRetryCount = 6;
    internal static readonly TimeSpan SqlServerMaxRetryDelay = TimeSpan.FromSeconds(10);
    private const int StaticAssetOneYearSeconds = 31_536_000;
    private const int StaticAssetOneWeekSeconds = 604_800;
    private const int StaticAssetOneDaySeconds = 86_400;

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var developmentCommand = TryParseDevelopmentCommand(args);
        builder.Configuration
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
            .AddJsonFile(
                $"appsettings.Local.{builder.Environment.EnvironmentName}.json",
                optional: true,
                reloadOnChange: true);

        var startup = ConfigureMiniPainterHubServices(builder);

        var app = builder.Build();

        if (startup.ConnectionResolution.ResolutionMessage is not null)
        {
            app.Logger.LogWarning("{Message}", startup.ConnectionResolution.ResolutionMessage);
        }

        if (developmentCommand is not null)
        {
            await RunDevelopmentCommandAsync(app, developmentCommand);
            return;
        }

        await RunMiniPainterHubStartupAsync(app, startup.HostedStartupConfiguration);
        ConfigureMiniPainterHubPipeline(app);

        app.Run();
    }
}

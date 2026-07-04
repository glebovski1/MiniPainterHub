using FluentAssertions;
using System;
using System.IO;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Architecture;

public class UiStructureTests
{
    [Theory]
    [InlineData("MiniPainterHub.WebApp/Shared/Viewer/RichImageViewer.razor", "MiniPainterHub.WebApp/Shared/Viewer/RichImageViewer.razor.cs")]
    [InlineData("MiniPainterHub.WebApp/Pages/Posts/PostDetails.razor", "MiniPainterHub.WebApp/Pages/Posts/PostDetails.razor.cs")]
    public void LargeComponents_KeepBehaviorInCodeBehind(string markupPath, string codeBehindPath)
    {
        var root = FindRepositoryRoot();
        var markup = Path.Combine(root, markupPath);
        var codeBehind = Path.Combine(root, codeBehindPath);

        File.Exists(markup).Should().BeTrue();
        File.Exists(codeBehind).Should().BeTrue();
        File.ReadAllText(markup).Should().NotContain("@code");
        File.ReadAllText(codeBehind).Should().Contain("partial class");
    }

    [Fact]
    public void GlobalCss_LoadsPolishStylesFromSeparateStylesheet()
    {
        var root = FindRepositoryRoot();
        var appCss = Path.Combine(root, "MiniPainterHub.WebApp/wwwroot/css/app.css");
        var polishCss = Path.Combine(root, "MiniPainterHub.WebApp/wwwroot/css/app-polish.css");
        var indexHtml = Path.Combine(root, "MiniPainterHub.WebApp/wwwroot/index.html");

        File.Exists(polishCss).Should().BeTrue();
        File.ReadAllText(appCss).Should().NotContain("Quiet Studio polish");
        File.ReadAllText(polishCss).Should().Contain("Quiet Studio polish");
        File.ReadAllText(indexHtml).Should().Contain("css/app-polish.css");
    }

    [Fact]
    public void PolishCss_DoesNotRedefineCoreDesignTokens()
    {
        var root = FindRepositoryRoot();
        var polishCss = Path.Combine(root, "MiniPainterHub.WebApp/wwwroot/css/app-polish.css");
        var source = File.ReadAllText(polishCss);

        source.Should().Contain("Quiet Studio polish");
        source.Should().NotContain("--canvas:");
        source.Should().NotContain("--surface:");
        source.Should().NotContain("--ink-900:");
        source.Should().NotContain("--primary-700:");
        source.Should().NotContain("--radius-lg:");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MiniPainterHub.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved.");
    }
}

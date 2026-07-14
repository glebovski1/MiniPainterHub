using FluentAssertions;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

    [Fact]
    public void BootstrapCompatibilityStylesKeepButtonsBadgesAndToastsOnBrand()
    {
        var root = FindRepositoryRoot();
        var appCss = File.ReadAllText(Path.Combine(root, "MiniPainterHub.WebApp/wwwroot/css/app.css"));
        var indexHtml = File.ReadAllText(Path.Combine(root, "MiniPainterHub.WebApp/wwwroot/index.html"));

        appCss.Should().MatchRegex(@"(?s)\.text-bg-light\s*\{[^}]*background-color:[^;}]+;[^}]*color:[^;}]+;");
        appCss.Should().Contain(".app-toast-container");
        appCss.Should().Contain(".btn-primary.active");
        appCss.Should().Contain(".btn-primary:active");
        appCss.Should().Contain(".btn-primary:focus");
        appCss.Should().Contain("rgba(31, 91, 82, 0.3)");
        appCss.Should().MatchRegex(@"(?s)\.btn-primary\.disabled,\s*\.btn-primary:disabled,\s*fieldset:disabled \.btn-primary\s*\{[^}]*background: rgba\(31, 91, 82, 0\.12\);[^}]*box-shadow: none;[^}]*opacity: 1;");
        indexHtml.Should().Contain("toast-container app-toast-container position-fixed end-0");
        indexHtml.Should().NotContain("toast-container position-fixed top-0");
        indexHtml.Should().Contain("getElementById('appHeader')");
        indexHtml.Should().Contain("getBoundingClientRect().bottom");
    }

    [Fact]
    public void BootstrapIconSubset_CoversEveryRazorIconUsage()
    {
        var root = FindRepositoryRoot();
        var webAppRoot = Path.Combine(root, "MiniPainterHub.WebApp");
        var css = File.ReadAllText(Path.Combine(webAppRoot, "wwwroot/css/bootstrap-icons-subset.css"));
        var iconPattern = new Regex(@"\bbi-([a-z0-9][a-z0-9-]*)\b", RegexOptions.IgnoreCase);

        var usedIcons = Directory
            .EnumerateFiles(webAppRoot, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => iconPattern.Matches(File.ReadAllText(file)).Cast<Match>())
            .Select(match => match.Groups[1].Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(icon => icon, StringComparer.Ordinal)
            .ToList();

        usedIcons.Should().NotBeEmpty();
        foreach (var icon in usedIcons)
        {
            css.Should().Contain($".bi-{icon}::before", $"the local icon subset must include bi-{icon}");
        }
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

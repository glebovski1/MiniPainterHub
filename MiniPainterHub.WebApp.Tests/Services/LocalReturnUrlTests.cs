using System;
using FluentAssertions;
using MiniPainterHub.WebApp.Services.Auth;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class LocalReturnUrlTests
{
    private static readonly Uri BaseUri = new("https://minipainterhub.example/");

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/support?ticket=4", "/support?ticket=4")]
    [InlineData("//evil.example/path", "/")]
    [InlineData("/\\evil", "/")]
    [InlineData("https://evil.example/path", "/")]
    [InlineData("https://minipainterhub.example/profile?tab=security", "/profile?tab=security")]
    public void Normalize_AcceptsOnlySameOriginApplicationPaths(string? value, string expected)
    {
        LocalReturnUrl.Normalize(value, BaseUri).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://minipainterhub.example/login?returnUrl=%2Fprofile%3Ftab%3Dsecurity", "returnUrl", "/profile?tab=security")]
    [InlineData("https://minipainterhub.example/auth/external/callback?error=cancelled", "error", "cancelled")]
    [InlineData("https://minipainterhub.example/account/sign-in-methods?linked=true", "linked", "true")]
    [InlineData("https://minipainterhub.example/login?other=value", "returnUrl", null)]
    public void GetQueryParameter_ReadsTheActualNavigationUri(string currentUri, string parameterName, string? expected)
    {
        LocalReturnUrl.GetQueryParameter(currentUri, parameterName).Should().Be(expected);
    }
}

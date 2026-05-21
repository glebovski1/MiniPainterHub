using System;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MiniPainterHub;
using Xunit;

namespace MiniPainterHub.Server.Tests;

public class ProgramEnvironmentTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Lighthouse", true)]
    [InlineData("Production", false)]
    public void IsLocalToolingEnvironment_ReturnsTrueOnlyForDevelopmentAndLighthouse(string environmentName, bool expected)
    {
        var environment = new TestHostEnvironment(environmentName);

        Program.IsLocalToolingEnvironment(environment).Should().Be(expected);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "MiniPainterHub.Server.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

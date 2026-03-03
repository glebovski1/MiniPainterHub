using FluentAssertions;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Threading.Tasks;
using Xunit;
using MiniPainterHub.Server.Exceptions;

namespace MiniPainterHub.Server.Tests.Services;

public class FeatureFlagsServiceTests
{
    [Fact]
    public async Task GetFlag_UsesDefault_WhenMissing_AndOverride_WhenPresent()
    {
        using var db = AppDbContextFactory.Create();
        var sut = new FeatureFlagsService(db);

        (await sut.GetFlagAsync("PostingEnabled", true)).Should().BeTrue();

        db.AppSettings.Add(new AppSetting { Key = "PostingEnabled", Value = "false", UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        (await sut.GetFlagAsync("PostingEnabled", true)).Should().BeFalse();
    }

    [Fact]
    public async Task SetFlag_UnknownFlagKey_ThrowsValidationException()
    {
        using var db = AppDbContextFactory.Create();
        var sut = new FeatureFlagsService(db);

        var act = async () => await sut.SetFlagAsync("SomeRandomFlag", "true", "admin");
        await act.Should().ThrowAsync<DomainValidationException>();
    }
}

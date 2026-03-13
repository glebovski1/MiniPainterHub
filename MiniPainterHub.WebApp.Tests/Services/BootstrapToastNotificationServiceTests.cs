using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.WebApp.Services.Notifications;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class BootstrapToastNotificationServiceTests
{
    [Fact]
    public async Task ShowValidationErrorsAsync_WhenErrorsAreEmpty_ShowsGenericValidationWarning()
    {
        var js = new RecordingJsRuntime();
        var service = new BootstrapToastNotificationService(js);

        await service.ShowValidationErrorsAsync(new Dictionary<string, string[]>());

        js.Invocations.Should().ContainSingle();
        js.Invocations[0].Identifier.Should().Be("bootstrapToaster.show");
        js.Invocations[0].Arguments[0].Should().Be("Validation failed.");
        js.Invocations[0].Arguments[1].Should().Be("Validation");
        js.Invocations[0].Arguments[2].Should().Be("warning");
    }

    [Fact]
    public async Task ShowValidationErrorsAsync_WhenErrorsExist_FormatsToastBody()
    {
        var js = new RecordingJsRuntime();
        var service = new BootstrapToastNotificationService(js);

        await service.ShowValidationErrorsAsync(new Dictionary<string, string[]>
        {
            ["Title"] = new[] { "Required." },
            [""] = new[] { "General failure." }
        });

        js.Invocations.Should().ContainSingle();
        js.Invocations[0].Identifier.Should().Be("bootstrapToaster.show");
        js.Invocations[0].Arguments[0].Should().Be(
            $"<strong>Title</strong>: Required.{System.Environment.NewLine}<strong>General</strong>: General failure.{System.Environment.NewLine}");
        js.Invocations[0].Arguments[1].Should().Be("Validation errors");
        js.Invocations[0].Arguments[2].Should().Be("warning");
    }
}

using System.Collections.Generic;

namespace MiniPainterHub.WebApp.Services.Notifications;

public interface INotificationService
{
    ValueTask ShowSuccessAsync(string message, string? header = null);
    ValueTask ShowInfoAsync(string message, string? header = null);
    ValueTask ShowWarningAsync(string message, string? header = null);
    ValueTask ShowErrorAsync(string message, string? header = null);
    ValueTask ShowValidationErrorsAsync(IDictionary<string, string[]> errors);
}

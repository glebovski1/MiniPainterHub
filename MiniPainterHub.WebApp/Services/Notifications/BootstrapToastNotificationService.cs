using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace MiniPainterHub.WebApp.Services.Notifications;

public sealed class BootstrapToastNotificationService : INotificationService
{
    private readonly IJSRuntime _jsRuntime;

    public BootstrapToastNotificationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public ValueTask ShowSuccessAsync(string message, string? header = null)
        => ShowAsync(message, header ?? "Success", "success");

    public ValueTask ShowInfoAsync(string message, string? header = null)
        => ShowAsync(message, header ?? "Info", "info");

    public ValueTask ShowWarningAsync(string message, string? header = null)
        => ShowAsync(message, header ?? "Warning", "warning");

    public ValueTask ShowErrorAsync(string message, string? header = null)
        => ShowAsync(message, header ?? "Error", "danger");

    public ValueTask ShowValidationErrorsAsync(IDictionary<string, string[]> errors)
    {
        if (errors.Count == 0)
        {
            return ShowWarningAsync("Validation failed.", "Validation");
        }

        var builder = new StringBuilder();
        foreach (var entry in errors)
        {
            var key = string.IsNullOrWhiteSpace(entry.Key) ? "General" : entry.Key;
            var messages = entry.Value ?? System.Array.Empty<string>();
            builder.AppendLine($"<strong>{key}</strong>: {string.Join(", ", messages.Where(m => !string.IsNullOrWhiteSpace(m)))}");
        }

        return ShowWarningAsync(builder.ToString(), "Validation errors");
    }

    private ValueTask ShowAsync(string message, string header, string style)
    {
        return _jsRuntime.InvokeVoidAsync("bootstrapToaster.show", message, header, style);
    }
}

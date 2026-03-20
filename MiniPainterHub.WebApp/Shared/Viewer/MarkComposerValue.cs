using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.WebApp.Shared.Viewer;

public sealed class MarkComposerValue
{
    [StringLength(64)]
    public string? Tag { get; set; }

    [StringLength(1000)]
    public string? Message { get; set; }

    public MarkComposerValue Clone() =>
        new()
        {
            Tag = Tag,
            Message = Message
        };
}

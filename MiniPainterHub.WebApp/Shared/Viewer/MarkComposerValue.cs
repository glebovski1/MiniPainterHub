namespace MiniPainterHub.WebApp.Shared.Viewer;

public sealed class MarkComposerValue
{
    public const int MaxTagLength = 64;
    public const int MaxMessageLength = 1000;

    public string? Tag { get; set; }

    public string? Message { get; set; }

    public MarkComposerValue Clone() =>
        new()
        {
            Tag = Tag,
            Message = Message
        };
}

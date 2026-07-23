namespace MiniPainterHub.Server.Options;

public sealed class DatabasePerformanceOptions
{
    public const string SectionName = "DatabasePerformance";

    public bool Enabled { get; set; } = true;

    public int ExcessiveCommandCount { get; set; } = 8;

    public int SlowRequestDatabaseMilliseconds { get; set; } = 250;

    public int PublicCacheSeconds { get; set; } = 10;
}

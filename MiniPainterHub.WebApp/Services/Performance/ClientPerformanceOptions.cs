namespace MiniPainterHub.WebApp.Services.Performance;

public sealed class ClientPerformanceOptions
{
    public bool Enabled { get; set; }

    public double SampleRate { get; set; } = 0.1;

    public int MaxBatchSize { get; set; } = 50;
}

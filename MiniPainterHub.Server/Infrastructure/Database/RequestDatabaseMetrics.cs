using System;
using System.Threading;

namespace MiniPainterHub.Server.Infrastructure.Database;

public sealed class RequestDatabaseMetrics
{
    private int _commandCount;
    private long _databaseTicks;

    public void RecordCommand(TimeSpan duration)
    {
        Interlocked.Increment(ref _commandCount);
        Interlocked.Add(ref _databaseTicks, duration.Ticks);
    }

    public RequestDatabaseMetricsSnapshot Snapshot() =>
        new(
            Volatile.Read(ref _commandCount),
            TimeSpan.FromTicks(Interlocked.Read(ref _databaseTicks)));
}

public readonly record struct RequestDatabaseMetricsSnapshot(int CommandCount, TimeSpan DatabaseDuration);

using FluentAssertions;
using MiniPainterHub.Server.Infrastructure.Database;
using System;
using Xunit;

namespace MiniPainterHub.Server.Tests.Infrastructure;

public sealed class RequestDatabaseMetricsTests
{
    [Fact]
    public void Snapshot_AccumulatesCommandCountAndDuration()
    {
        var metrics = new RequestDatabaseMetrics();

        metrics.RecordCommand(TimeSpan.FromMilliseconds(12));
        metrics.RecordCommand(TimeSpan.FromMilliseconds(8));

        var snapshot = metrics.Snapshot();
        snapshot.CommandCount.Should().Be(2);
        snapshot.DatabaseDuration.Should().Be(TimeSpan.FromMilliseconds(20));
    }
}

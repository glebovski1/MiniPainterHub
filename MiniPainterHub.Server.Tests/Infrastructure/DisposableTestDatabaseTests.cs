using FluentAssertions;
using MiniPainterHub.Server.Infrastructure.Database;
using System;
using Xunit;

namespace MiniPainterHub.Server.Tests.Infrastructure;

public class DisposableTestDatabaseTests
{
    [Theory]
    [InlineData("Server=tcp:127.0.0.1,1433;Database=MiniPainterHub_E2E_123")]
    [InlineData("Data Source=localhost;Initial Catalog=MiniPainterHub_RuntimeUx_123")]
    [InlineData("Server=tcp:[::1],1433;Database=MiniPainterHub_Lighthouse_123")]
    public void LoopbackDisposableDatabase_IsAccepted(string connectionString)
    {
        DisposableTestDatabase.ValidateConnectionString(connectionString);
    }

    [Theory]
    [InlineData("Server=production.database.windows.net;Database=MiniPainterHub_E2E")]
    [InlineData("Server=127.0.0.1;Database=MiniPainter-database")]
    [InlineData("Server=localhost.evil.example;Database=MiniPainterHub_E2E")]
    [InlineData("Server=localhost;Database=master")]
    [InlineData("Server=localhost;Database=MiniPainterHub_E2E;Failover Partner=remote")]
    [InlineData("Server=localhost;Database=MiniPainterHub_E2E;AttachDbFilename=C:\\data.mdf")]
    [InlineData("Server=localhost;Database=MiniPainterHub_E2E;User Instance=True")]
    [InlineData("Server=localhost;Database=MiniPainterHub_E2E;Data Source=remote")]
    public void UnsafeResetTarget_IsRejected(string connectionString)
    {
        Action validate = () => DisposableTestDatabase.ValidateConnectionString(connectionString);
        validate.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LocalDb_IsAcceptedOnlyOnWindows()
    {
        Action validate = () => DisposableTestDatabase.ValidateConnectionString(
            "Server=(localdb)\\MSSQLLocalDB;Database=MiniPainterHub_E2E");
        if (OperatingSystem.IsWindows())
        {
            validate.Should().NotThrow();
        }
        else
        {
            validate.Should().Throw<InvalidOperationException>();
        }
    }
}

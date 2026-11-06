using FluentAssertions;
using Microsoft.Data.SqlClient;
using System;
using Xunit;

namespace MiniPainterHub.Server.Tests.Infrastructure;

public class SqlServerTestDatabaseTests
{
    [Theory]
    [InlineData("Server=production.database.windows.net;Database=Production")]
    [InlineData("Server=localhost;Failover Partner=production.database.windows.net")]
    public void RemoteDatabaseTargetsAreRejectedBeforeOpeningConnection(string connection)
    {
        Action create = () => SqlServerTestDatabase.CreateConnectionString("Safety", connection);
        create.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExplicitSqlAlwaysUsesUniqueDisposableCatalog()
    {
        const string configured = "Server=tcp:127.0.0.1,1433;Database=master;User ID=sa;Password=fixture";
        var first = new SqlConnectionStringBuilder(SqlServerTestDatabase.CreateConnectionString("Safety", configured));
        var second = new SqlConnectionStringBuilder(SqlServerTestDatabase.CreateConnectionString("Safety", configured));
        first.InitialCatalog.Should().StartWith("MiniPainterHub_Safety_").And.NotBe(second.InitialCatalog);
        first.DataSource.Should().Be("tcp:127.0.0.1,1433");
    }
}

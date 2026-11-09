using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Data;
using System;
using System.Data;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace MiniPainterHub.Server.Tests.Infrastructure;

internal static class SqlServerTestDatabase
{
    internal const string ConnectionVariable = "MPH_TEST_SQL_CONNECTION_STRING";
    private const string CompatibilityVariable = "MPH_TEST_SQL_EXPECTED_COMPATIBILITY_LEVEL";
    private const string ProductionCollation = "SQL_Latin1_General_CP1_CI_AS";

    internal static string CreateConnectionString(string scenario, string? configured = null)
    {
        configured ??= Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new InvalidOperationException($"{ConnectionVariable} must specify the disposable SQL Server for relational tests on this OS.");
            }

            configured = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";
        }

        var builder = new SqlConnectionStringBuilder(configured);
        var server = builder.DataSource;
        var localDb = server.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase);
        var tcpHost = server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase) ? server[4..] : server;
        tcpHost = tcpHost.Split(',')[0];
        var loopback = tcpHost.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || tcpHost == "127.0.0.1" || tcpHost == "[::1]";
        if (((!localDb || !OperatingSystem.IsWindows()) && !loopback)
            || !string.IsNullOrEmpty(builder.FailoverPartner))
        {
            throw new InvalidOperationException("Relational test databases must use Windows LocalDB or a disposable SQL Server on loopback. Remote SQL is not a test target.");
        }

        builder.InitialCatalog = $"MiniPainterHub_{scenario}_{Guid.NewGuid():N}";
        builder.AttachDBFilename = string.Empty;
        builder.MultipleActiveResultSets = true;
        return builder.ConnectionString;
    }

    internal static async Task VerifyAsync(AppDbContext context, ITestOutputHelper output)
    {
        if (!context.Database.IsSqlServer())
        {
            throw new InvalidOperationException("Relational tests require the SQL Server provider.");
        }

        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT compatibility_level, collation_name FROM sys.databases WHERE name = DB_NAME()";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Could not verify the test database's SQL metadata.");
        }

        var compatibility = Convert.ToInt32(reader.GetValue(0));
        var collation = reader.GetString(1);
        var expected = Environment.GetEnvironmentVariable(CompatibilityVariable);
        if (!string.IsNullOrWhiteSpace(expected)
            && (!int.TryParse(expected, out var expectedLevel) || compatibility != expectedLevel))
        {
            throw new InvalidOperationException("SQL test compatibility level does not match the explicitly required level.");
        }

        if (!string.IsNullOrWhiteSpace(expected) && collation != ProductionCollation)
        {
            throw new InvalidOperationException("SQL test collation does not match production.");
        }

        var operatingSystem = OperatingSystem.IsLinux() ? "Linux" : "Windows";
        output.WriteLine($"Provider={context.Database.ProviderName}; OperatingSystem={operatingSystem}; ServerVersion={connection.ServerVersion}; Database={connection.Database}; Compatibility={compatibility}; Collation={collation}");
    }
}

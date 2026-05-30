using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Server.Data;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MiniPainterHub;

public partial class Program
{
    internal static void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder sqlOpts)
    {
        sqlOpts.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)
            .MigrationsHistoryTable("__EFMigrationsHistory", "dbo")
            .EnableRetryOnFailure(
                maxRetryCount: SqlServerMaxRetryCount,
                maxRetryDelay: SqlServerMaxRetryDelay,
                errorNumbersToAdd: null);
    }

    internal static bool IsLocalToolingEnvironment(IHostEnvironment environment) =>
        environment.IsDevelopment()
        || environment.IsEnvironment(LighthouseEnvironmentName);

    private static async Task EnsureDevelopmentDatabaseAsync(AppDbContext db, ILogger logger, IConfiguration configuration)
    {
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (SqlException ex) when (IsDuplicateIdentitySchemaError(ex) && ShouldRecreateOnSchemaConflict(configuration))
        {
            logger.LogWarning(
                ex,
                "Detected schema conflict while applying migrations in Development. Recreating database and retrying migration.");
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }
    }

    private static bool IsDuplicateIdentitySchemaError(SqlException ex) =>
        ex.Number == 2714 && ex.Message.Contains("AspNetRoles", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldRecreateOnSchemaConflict(IConfiguration configuration) =>
        configuration.GetValue<bool?>("Database:RecreateOnSchemaConflict") ?? true;

    private static ConnectionResolution ResolveDevelopmentConnectionString(
        IHostEnvironment environment,
        IConfiguration configuration,
        string? configuredConnection)
    {
        if (!IsLocalToolingEnvironment(environment)
            || !OperatingSystem.IsWindows()
            || string.IsNullOrWhiteSpace(configuredConnection)
            || !IsLocalDbConnection(configuredConnection))
        {
            return new ConnectionResolution(configuredConnection, null);
        }

        if (CanOpenSqlConnection(BuildProbeConnectionString(configuredConnection)))
        {
            return new ConnectionResolution(configuredConnection, null);
        }

        if (TryStartLocalDbInstance(configuredConnection, out var localDbStartMessage)
            && CanOpenSqlConnection(BuildProbeConnectionString(configuredConnection)))
        {
            return new ConnectionResolution(
                configuredConnection,
                localDbStartMessage);
        }

        if (AllowSqlExpressFallbackInDevelopment(configuration))
        {
            var sqlExpressConnection = TryCreateSqlExpressFallbackConnectionString(configuredConnection);
            if (sqlExpressConnection is not null && CanOpenSqlConnection(BuildProbeConnectionString(sqlExpressConnection)))
            {
                return new ConnectionResolution(
                    sqlExpressConnection,
                    "Configured LocalDB instance was unavailable. Falling back to .\\SQLEXPRESS for Development.");
            }
        }

        return new ConnectionResolution(
            configuredConnection,
            "Configured LocalDB instance was unavailable. Development will keep using LocalDB so your existing MiniPainterHub data stays on the expected database.");
    }

    private static bool AllowSqlExpressFallbackInDevelopment(IConfiguration configuration) =>
        configuration.GetValue<bool>("Database:AllowSqlExpressFallbackInDevelopment");

    private static bool IsLocalDbConnection(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.DataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string BuildProbeConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
            ConnectTimeout = 3,
            Pooling = false
        };

        builder.AttachDBFilename = string.Empty;

        return builder.ConnectionString;
    }

    private static bool TryStartLocalDbInstance(string connectionString, out string message)
    {
        message = string.Empty;

        var instanceName = TryGetLocalDbInstanceName(connectionString);
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sqllocaldb",
                Arguments = $"start {instanceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                message = $"Timed out while starting LocalDB instance '{instanceName}'.";
                return false;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            if (process.ExitCode != 0)
            {
                message = string.IsNullOrWhiteSpace(error)
                    ? $"Failed to start LocalDB instance '{instanceName}'."
                    : $"Failed to start LocalDB instance '{instanceName}': {error}";
                return false;
            }

            message = string.IsNullOrWhiteSpace(output)
                ? $"Started LocalDB instance '{instanceName}' for Development."
                : $"Started LocalDB instance '{instanceName}' for Development: {output}";
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            message = $"Failed to launch sqllocaldb for '{instanceName}': {ex.Message}";
            return false;
        }
    }

    private static string? TryGetLocalDbInstanceName(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var dataSource = builder.DataSource;
            if (!dataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return dataSource[(dataSource.IndexOf('\\') + 1)..];
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? TryCreateSqlExpressFallbackConnectionString(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                DataSource = @".\SQLEXPRESS",
                ConnectTimeout = 3,
                TrustServerCertificate = true
            };

            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                builder.InitialCatalog = "MiniPainterHub";
            }

            builder.AttachDBFilename = string.Empty;

            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool CanOpenSqlConnection(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void LogDatabaseTarget(AppDbContext db, ILogger logger)
    {
        var providerName = db.Database.ProviderName ?? "unknown";

        if (!db.Database.IsRelational())
        {
            logger.LogInformation("Using database provider {Provider}.", providerName);
            return;
        }

        var connection = db.Database.GetDbConnection();
        logger.LogInformation(
            "Using database provider {Provider}. DataSource={DataSource}; Database={Database}",
            providerName,
            connection.DataSource,
            connection.Database);
    }
    private sealed record ConnectionResolution(string? ConnectionString, string? ResolutionMessage);
}

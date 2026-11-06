using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Data;
using System;
using System.Text.RegularExpressions;

namespace MiniPainterHub.Server.Infrastructure.Database;

internal static class DisposableTestDatabase
{
    internal static void Validate(AppDbContext db)
    {
        if (db.Database.IsRelational())
        {
            ValidateConnectionString(db.Database.GetConnectionString());
        }
    }

    internal static void ValidateConnectionString(string? connectionString)
    {
        var connection = new SqlConnectionStringBuilder(connectionString);
        var localDb = OperatingSystem.IsWindows()
            && Regex.IsMatch(connection.DataSource, @"^\(localdb\)\\[A-Za-z0-9_-]+$", RegexOptions.IgnoreCase);
        var loopback = Regex.IsMatch(connection.DataSource,
            @"^(tcp:)?(127\.0\.0\.1|localhost|\[::1\])(,[0-9]{1,5})?$", RegexOptions.IgnoreCase);
        var disposableName = Regex.IsMatch(connection.InitialCatalog,
            @"^MiniPainterHub_(E2E|RuntimeUx|Lighthouse)(_[A-Za-z0-9_]+)?$");

        if ((!localDb && !loopback) || !disposableName
            || !string.IsNullOrEmpty(connection.AttachDBFilename)
            || !string.IsNullOrEmpty(connection.FailoverPartner) || connection.UserInstance)
        {
            throw new InvalidOperationException(
                "Reset-enabled tooling requires loopback SQL (or Windows LocalDB) and a disposable "
                + "MiniPainterHub_E2E, MiniPainterHub_RuntimeUx or MiniPainterHub_Lighthouse database. "
                + "Attach files, user instances and failover partners are not allowed.");
        }
    }
}

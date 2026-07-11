using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.Database.Context.Data.Interceptors;

public class SqliteWalInterceptor(ILogger<SqliteWalInterceptor> logger) : DbConnectionInterceptor
{
    private const int BusyTimeoutMs = 15000;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        TrySetPragmas(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await TrySetPragmasAsync(connection, cancellationToken);
    }

    private void TrySetPragmas(DbConnection connection)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA journal_mode=WAL; PRAGMA busy_timeout={BusyTimeoutMs}; PRAGMA synchronous=NORMAL;";
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply SQLite WAL pragmas on connection open");
        }
    }

    private async Task TrySetPragmasAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA journal_mode=WAL; PRAGMA busy_timeout={BusyTimeoutMs}; PRAGMA synchronous=NORMAL;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply SQLite WAL pragmas on connection open");
        }
    }
}

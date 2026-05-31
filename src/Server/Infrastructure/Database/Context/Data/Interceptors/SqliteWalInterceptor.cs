using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace K7.Server.Infrastructure.Database.Context.Data.Interceptors;

public class SqliteWalInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        TrySetPragmas(connection);
    }

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TrySetPragmas(connection);
        return Task.CompletedTask;
    }

    private static void TrySetPragmas(DbConnection connection)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL;";
            command.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // Silently ignore - the DB may not be writable yet (e.g. during existence check).
            // PRAGMAs will succeed on subsequent connections once the DB is created.
        }
    }
}

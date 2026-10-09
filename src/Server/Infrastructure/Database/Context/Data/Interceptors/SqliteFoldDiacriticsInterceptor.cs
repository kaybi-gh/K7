using System.Data.Common;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace K7.Server.Infrastructure.Database.Context.Data.Interceptors;

public sealed class SqliteFoldDiacriticsInterceptor : DbConnectionInterceptor
{
    public static void Register(DbConnection connection)
    {
        if (connection is not SqliteConnection sqlite)
            return;

        sqlite.CreateFunction(
            TextSearchFunctions.FoldDiacriticsFunctionName,
            (string? value) => value is null ? null : MediaTextSearchHelper.RemoveDiacritics(value),
            isDeterministic: true);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Register(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Register(connection);
        return Task.CompletedTask;
    }
}

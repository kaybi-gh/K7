using Microsoft.Data.Sqlite;

namespace K7.Import.Sources.Plex;

internal sealed class PlexLibraryDb
{
    private readonly Dictionary<string, Dictionary<string, double>> _ratingsByAccount;
    private readonly Dictionary<string, string> _accountIdByAlias;

    private PlexLibraryDb(
        Dictionary<string, Dictionary<string, double>> ratingsByAccount,
        Dictionary<string, string> accountIdByAlias)
    {
        _ratingsByAccount = ratingsByAccount;
        _accountIdByAlias = accountIdByAlias;
    }

    public IReadOnlyList<string> RatingCountLabels =>
        _ratingsByAccount
            .OrderByDescending(pair => pair.Value.Count)
            .Select(pair =>
            {
                var name = _accountIdByAlias
                    .FirstOrDefault(alias =>
                        string.Equals(alias.Value, pair.Key, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(alias.Key, pair.Key, StringComparison.OrdinalIgnoreCase))
                    .Key;
                return string.IsNullOrWhiteSpace(name)
                    ? $"{pair.Key}={pair.Value.Count}"
                    : $"{pair.Key} ({name})={pair.Value.Count}";
            })
            .ToList();

    public IReadOnlyList<(string AccountId, string? Name, int Count)> RatedAccounts =>
        _ratingsByAccount
            .OrderByDescending(pair => pair.Value.Count)
            .Select(pair =>
            {
                var name = _accountIdByAlias
                    .FirstOrDefault(alias =>
                        string.Equals(alias.Value, pair.Key, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(alias.Key, pair.Key, StringComparison.OrdinalIgnoreCase))
                    .Key;
                return (pair.Key, string.IsNullOrWhiteSpace(name) ? null : name, pair.Value.Count);
            })
            .ToList();

    public IReadOnlyDictionary<string, double> RatingsFor(params string?[] accountIdOrNames)
    {
        foreach (var key in accountIdOrNames)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (_ratingsByAccount.TryGetValue(key, out var direct))
                return direct;

            if (_accountIdByAlias.TryGetValue(key, out var accountId)
                && _ratingsByAccount.TryGetValue(accountId, out var mapped))
            {
                return mapped;
            }
        }

        return new Dictionary<string, double>(StringComparer.Ordinal);
    }

    public static PlexLibraryDb Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException(
                "Plex library database not found. Copy com.plexapp.plugins.library.db from the Plex server.",
                path);

        var ratings = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT CAST(mis.account_id AS TEXT) AS account_id,
                       CAST(mi.id AS TEXT) AS rating_key,
                       mis.rating
                FROM metadata_item_settings mis
                INNER JOIN metadata_items mi ON mi.guid = mis.guid
                WHERE mis.account_id IS NOT NULL
                  AND mis.rating IS NOT NULL
                  AND mis.rating > 0
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var accountId = reader.GetString(0);
                var ratingKey = reader.GetString(1);
                var rating = reader.GetDouble(2);
                if (!ratings.TryGetValue(accountId, out var byKey))
                {
                    byKey = new Dictionary<string, double>(StringComparer.Ordinal);
                    ratings[accountId] = byKey;
                }

                byKey[ratingKey] = rating;
            }
        }

        TryLoadAccountAliases(connection, aliases);
        return new PlexLibraryDb(ratings, aliases);
    }

    private static void TryLoadAccountAliases(SqliteConnection connection, Dictionary<string, string> aliases)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT CAST(id AS TEXT) AS id, name FROM accounts WHERE name IS NOT NULL AND name <> ''";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var name = reader.GetString(1);
                aliases.TryAdd(id, id);
                aliases.TryAdd(name, id);
            }
        }
        catch (SqliteException)
        {
            // Older or unexpected Plex schemas may not have accounts.
        }
    }
}

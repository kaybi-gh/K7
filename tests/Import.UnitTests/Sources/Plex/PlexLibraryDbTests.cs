using K7.Import.Sources.Plex;
using Microsoft.Data.Sqlite;

namespace K7.Import.UnitTests.Sources.Plex;

[TestFixture]
public class PlexLibraryDbTests
{
    [Test]
    public void Load_ShouldGroupRatingsByAccountId()
    {
        var path = Path.Combine(Path.GetTempPath(), "k7-plex-library-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE metadata_items (
                        id INTEGER PRIMARY KEY,
                        guid TEXT
                    );
                    CREATE TABLE metadata_item_settings (
                        account_id INTEGER,
                        guid TEXT,
                        rating REAL
                    );
                    CREATE TABLE accounts (
                        id INTEGER PRIMARY KEY,
                        name TEXT
                    );
                    INSERT INTO metadata_items (id, guid) VALUES
                        (10, 'plex://movie/a'),
                        (11, 'plex://movie/b'),
                        (12, 'plex://movie/c');
                    INSERT INTO metadata_item_settings (account_id, guid, rating) VALUES
                        (1, 'plex://movie/a', 8),
                        (1, 'plex://movie/b', 9),
                        (2, 'plex://movie/c', 7),
                        (2, 'plex://movie/missing', 10),
                        (1, 'plex://movie/a', 0);
                    INSERT INTO accounts (id, name) VALUES
                        (1, 'Kaybi'),
                        (2, 'Charlotte');
                    """;
                command.ExecuteNonQuery();
            }

            var db = PlexLibraryDb.Load(path);

            db.RatingCountLabels.Should().Contain("1 (Kaybi)=2");
            db.RatingCountLabels.Should().Contain("2 (Charlotte)=1");
            db.RatingsFor("Charlotte").Should().ContainKey("12").WhoseValue.Should().Be(7);
            db.RatingsFor("2").Should().HaveCount(1);
            db.RatingsFor("1").Should().HaveCount(2);
            db.RatingsFor("999").Should().BeEmpty();
            db.RatedAccounts.Should().Contain(account => account.AccountId == "2" && account.Name == "Charlotte");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public void Load_ShouldThrow_WhenFileIsMissing()
    {
        var act = () => PlexLibraryDb.Load(Path.Combine(Path.GetTempPath(), "missing-plex.db"));
        act.Should().Throw<FileNotFoundException>();
    }
}

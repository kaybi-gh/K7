using Microsoft.EntityFrameworkCore;

namespace K7.Clients.MAUI.Data;

/// <summary>
/// Creates the offline SQLite schema off the UI thread. Callers must await
/// <see cref="Ready"/> before opening a context so the first query does not race EnsureCreated.
/// </summary>
internal static class OfflineDbBootstrap
{
    private static Task _ready = Task.CompletedTask;

    public static Task Ready => _ready;

    public static void Start(IDbContextFactory<OfflineMediaDbContext> factory)
    {
        _ready = Task.Run(() => Initialize(factory));
    }

    private static void Initialize(IDbContextFactory<OfflineMediaDbContext> factory)
    {
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();

        try
        {
            db.Database.ExecuteSqlRaw(
                "ALTER TABLE DownloadedMedia ADD COLUMN LastPlaybackPosition REAL NOT NULL DEFAULT 0");
        }
        catch
        {
            // Column already exists
        }

        try
        {
            db.Database.ExecuteSqlRaw(
                "ALTER TABLE PendingPlaybackEvents ADD COLUMN SharedProfileId TEXT NULL");
        }
        catch
        {
            // Column already exists
        }

        try
        {
            db.Database.ExecuteSqlRaw(
                "ALTER TABLE PendingPlaybackEvents ADD COLUMN IdentityUserId TEXT NOT NULL DEFAULT ''");
        }
        catch
        {
            // Column already exists
        }

        // Pre-attribution rows cannot be sent safely to any later signed-in user.
        db.Database.ExecuteSqlRaw(
            "DELETE FROM PendingPlaybackEvents WHERE IsSynced = 0 AND (IdentityUserId IS NULL OR IdentityUserId = '')");
    }
}

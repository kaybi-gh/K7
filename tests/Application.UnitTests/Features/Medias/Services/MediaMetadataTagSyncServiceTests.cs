using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

[TestFixture]
public class MediaMetadataTagSyncServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ApplyTagsAsync_ShouldReusePendingTag_WhenSameGenreAppliedToMultipleMediasBeforeSave()
    {
        var now = DateTimeOffset.UtcNow;
        var album = new MusicAlbum
        {
            Id = Guid.NewGuid(),
            Title = "Demo Album",
            Created = now,
            LastModified = now
        };
        var firstTrack = new MusicTrack
        {
            Id = Guid.NewGuid(),
            Title = "Track 1",
            AlbumId = album.Id,
            Created = now,
            LastModified = now
        };
        var secondTrack = new MusicTrack
        {
            Id = Guid.NewGuid(),
            Title = "Track 2",
            AlbumId = album.Id,
            Created = now,
            LastModified = now
        };

        _context.Medias.AddRange(album, firstTrack, secondTrack);

        var service = new MediaMetadataTagSyncService(_context);
        var desired = MetadataTagBuilder.FromGenres(firstTrack, ["Classical"]);

        await service.ApplyTagsAsync(album, desired, CancellationToken.None);
        await service.ApplyTagsAsync(firstTrack, desired, CancellationToken.None);
        await service.ApplyTagsAsync(secondTrack, desired, CancellationToken.None);

        await _context.SaveChangesAsync();

        var tags = await _context.MetadataTags
            .Where(t => t.Kind == MetadataTagKind.Genre && t.NormalizedKey == "classical")
            .ToListAsync();

        tags.Should().ContainSingle();
    }

    [Test]
    public async Task ApplyTagsAsync_ShouldReuseExistingDbTag_WhenClassicalAlreadyExists()
    {
        var now = DateTimeOffset.UtcNow;
        var existingTag = new MetadataTag
        {
            Id = Guid.NewGuid(),
            Kind = MetadataTagKind.Genre,
            NormalizedKey = "classical",
            DisplayName = "Classical"
        };
        _context.MetadataTags.Add(existingTag);
        await _context.SaveChangesAsync();

        var album = new MusicAlbum
        {
            Id = Guid.NewGuid(),
            Title = "Demo Album",
            Created = now,
            LastModified = now
        };
        var track = new MusicTrack
        {
            Id = Guid.NewGuid(),
            Title = "Track 1",
            AlbumId = album.Id,
            Created = now,
            LastModified = now
        };
        _context.Medias.AddRange(album, track);

        IApplicationDbContext dbContext = _context;
        var service = new MediaMetadataTagSyncService(dbContext);
        var desired = MetadataTagBuilder.FromGenres(album, ["Classical"]);

        await service.ApplyTagsAsync(album, desired, CancellationToken.None);
        await service.ApplyTagsAsync(track, desired, CancellationToken.None);

        await _context.SaveChangesAsync();

        var tags = await _context.MetadataTags
            .Where(t => t.Kind == MetadataTagKind.Genre && t.NormalizedKey == "classical")
            .ToListAsync();

        tags.Should().ContainSingle();
        tags[0].Id.Should().Be(existingTag.Id);
    }
}

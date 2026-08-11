using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MusicOrphanCleanupHelperTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ILogger _logger = null!;

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
        _logger = Substitute.For<ILogger>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task TryDeleteTrackIfOrphanAsync_ShouldDeleteTrackAndEmptyAlbum_WhenNoFilesAndNoUserData()
    {
        var (albumId, trackId, artistId) = await SeedOrphanTrackAsync();

        var deleted = await MusicOrphanCleanupHelper.TryDeleteTrackIfOrphanAsync(_context, trackId, _logger);
        await _context.SaveChangesAsync();

        deleted.Should().BeTrue();
        (await _context.Medias.OfType<MusicTrack>().AnyAsync(t => t.Id == trackId)).Should().BeFalse();
        (await _context.Medias.OfType<MusicAlbum>().AnyAsync(a => a.Id == albumId)).Should().BeFalse();
        (await _context.Medias.OfType<MusicArtist>().AnyAsync(a => a.Id == artistId)).Should().BeFalse();
    }

    [Test]
    public async Task TryDeleteTrackIfOrphanAsync_ShouldKeepArtist_WhenArtistHasUserData()
    {
        var (albumId, trackId, artistId) = await SeedOrphanTrackAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = artistId,
            PlayCount = 1
        });
        await _context.SaveChangesAsync();

        var deleted = await MusicOrphanCleanupHelper.TryDeleteTrackIfOrphanAsync(_context, trackId, _logger);
        await _context.SaveChangesAsync();

        deleted.Should().BeTrue();
        (await _context.Medias.OfType<MusicTrack>().AnyAsync(t => t.Id == trackId)).Should().BeFalse();
        (await _context.Medias.OfType<MusicAlbum>().AnyAsync(a => a.Id == albumId)).Should().BeFalse();
        (await _context.Medias.OfType<MusicArtist>().AnyAsync(a => a.Id == artistId)).Should().BeTrue();
    }

    [Test]
    public async Task TryDeleteTrackIfOrphanAsync_ShouldKeepArtist_WhenCreditRemains()
    {
        var (albumId, trackId, artistId) = await SeedOrphanTrackAsync();
        var otherAlbum = new MusicAlbum { Id = Guid.NewGuid(), Title = "Other" };
        var otherTrack = new MusicTrack
        {
            Id = Guid.NewGuid(),
            Title = "Other Track",
            AlbumId = otherAlbum.Id,
            Album = otherAlbum
        };
        _context.Medias.Add(otherAlbum);
        _context.Medias.Add(otherTrack);
        _context.MusicArtistCredits.Add(new MusicArtistCredit
        {
            Id = Guid.NewGuid(),
            MediaId = otherTrack.Id,
            MusicArtistId = artistId,
            Order = 0
        });
        await _context.SaveChangesAsync();

        var deleted = await MusicOrphanCleanupHelper.TryDeleteTrackIfOrphanAsync(_context, trackId, _logger);
        await _context.SaveChangesAsync();

        deleted.Should().BeTrue();
        (await _context.Medias.OfType<MusicAlbum>().AnyAsync(a => a.Id == albumId)).Should().BeFalse();
        (await _context.Medias.OfType<MusicArtist>().AnyAsync(a => a.Id == artistId)).Should().BeTrue();
    }

    [Test]
    public async Task TryDeleteTrackIfOrphanAsync_ShouldKeepTrack_WhenIndexedFileRemains()
    {
        var (_, trackId, _) = await SeedOrphanTrackAsync(withFile: true);

        var deleted = await MusicOrphanCleanupHelper.TryDeleteTrackIfOrphanAsync(_context, trackId, _logger);

        deleted.Should().BeFalse();
        (await _context.Medias.OfType<MusicTrack>().AnyAsync(t => t.Id == trackId)).Should().BeTrue();
    }

    [Test]
    public async Task TryDeleteTrackIfOrphanAsync_ShouldKeepTrack_WhenUserDataExists()
    {
        var (_, trackId, _) = await SeedOrphanTrackAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = trackId,
            PlayCount = 1
        });
        await _context.SaveChangesAsync();

        var deleted = await MusicOrphanCleanupHelper.TryDeleteTrackIfOrphanAsync(_context, trackId, _logger);

        deleted.Should().BeFalse();
        (await _context.Medias.OfType<MusicTrack>().AnyAsync(t => t.Id == trackId)).Should().BeTrue();
    }

    [Test]
    public async Task TryDeleteAlbumIfOrphanAsync_ShouldKeepAlbum_WhenCollectionItemExists()
    {
        var (albumId, trackId, _) = await SeedOrphanTrackAsync();
        _context.Medias.Remove(await _context.Medias.OfType<MusicTrack>().SingleAsync(t => t.Id == trackId));
        await _context.SaveChangesAsync();

        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        var collection = new Collection { Id = Guid.NewGuid(), Title = "Favs", UserId = userId };
        _context.Collections.Add(collection);
        _context.CollectionItems.Add(new CollectionItem
        {
            CollectionId = collection.Id,
            MediaId = albumId,
            Order = 0
        });
        await _context.SaveChangesAsync();

        var deleted = await MusicOrphanCleanupHelper.TryDeleteAlbumIfOrphanAsync(_context, albumId, _logger);

        deleted.Should().BeFalse();
        (await _context.Medias.OfType<MusicAlbum>().AnyAsync(a => a.Id == albumId)).Should().BeTrue();
    }

    private async Task<(Guid AlbumId, Guid TrackId, Guid ArtistId)> SeedOrphanTrackAsync(bool withFile = false)
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Music",
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Music",
            MediaType = LibraryMediaType.Music,
            RootPath = "/music",
            MetadataProviderName = "musicbrainz",
            MetadataLanguage = "en",
            MetadataFallbackLanguage = "en"
        });

        var artistId = Guid.NewGuid();
        var albumId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var artist = new MusicArtist { Id = artistId, Title = "Artist" };
        var album = new MusicAlbum { Id = albumId, Title = "Album", ArtistId = artistId, Artist = artist };
        var track = new MusicTrack
        {
            Id = trackId,
            Title = "Track",
            AlbumId = albumId,
            Album = album
        };
        _context.Medias.Add(artist);
        _context.Medias.Add(album);
        _context.Medias.Add(track);

        if (withFile)
        {
            _context.IndexedFiles.Add(new IndexedFile
            {
                Id = Guid.NewGuid(),
                LibraryId = libraryId,
                Path = "/music/a/01.flac",
                Name = "01.flac",
                Extension = ".flac",
                Hash = 1,
                Size = 1,
                MediaId = trackId
            });
        }

        await _context.SaveChangesAsync();
        return (albumId, trackId, artistId);
    }
}

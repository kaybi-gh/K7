using K7.Server.Application.Common.Security;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Shares;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.MediaShares;

/// <summary>
/// In-memory database seeded with a series, a music artist and a movie, for share tests.
/// </summary>
public sealed class MediaShareTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public MediaShareTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new ApplicationDbContext(options);
        Context.Database.EnsureCreated();
        Seed();
    }

    public ApplicationDbContext Context { get; }

    public Guid CreatorId { get; } = Guid.NewGuid();
    public Guid LibraryId { get; } = Guid.NewGuid();

    public Guid SerieId { get; } = Guid.NewGuid();
    public Guid Season1Id { get; } = Guid.NewGuid();
    public Guid Season2Id { get; } = Guid.NewGuid();
    public Guid Episode1Id { get; } = Guid.NewGuid();
    public Guid Episode2Id { get; } = Guid.NewGuid();
    public Guid OtherSeasonEpisodeId { get; } = Guid.NewGuid();

    public Guid ArtistId { get; } = Guid.NewGuid();
    public Guid OtherArtistId { get; } = Guid.NewGuid();
    public Guid AlbumId { get; } = Guid.NewGuid();
    public Guid TrackId { get; } = Guid.NewGuid();
    public Guid OtherArtistAlbumId { get; } = Guid.NewGuid();

    /// <summary>On the other artist's album, but credited to <see cref="ArtistId"/>.</summary>
    public Guid GuestTrackId { get; } = Guid.NewGuid();

    public Guid MovieId { get; } = Guid.NewGuid();
    public Guid MovieFileId { get; } = Guid.NewGuid();
    public Guid Episode1FileId { get; } = Guid.NewGuid();
    public Guid OrphanFileId { get; } = Guid.NewGuid();

    public MediaShare AddShare(Guid rootMediaId, string token, Action<MediaShare>? configure = null)
    {
        var share = new MediaShare
        {
            Id = Guid.NewGuid(),
            RootMediaId = rootMediaId,
            TokenHash = ShareTokenHelper.Hash(token),
            CreatedByUserId = CreatorId
        };
        configure?.Invoke(share);
        Context.MediaShares.Add(share);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        return share;
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }

    private void Seed()
    {
        var groupId = Guid.NewGuid();
        Context.Users.Add(new User { Id = CreatorId, DisplayName = "creator" });
        Context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "All",
            MediaType = LibraryMediaType.Movie
        });
        Context.Libraries.Add(new Library
        {
            Id = LibraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Movie,
            Title = "All",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

        Context.Medias.AddRange(
            new Serie { Id = SerieId, Title = "Serie" },
            new SerieSeason { Id = Season1Id, SerieId = SerieId, SeasonNumber = 1 },
            new SerieSeason { Id = Season2Id, SerieId = SerieId, SeasonNumber = 2 },
            new SerieEpisode { Id = Episode1Id, SerieId = SerieId, SeasonId = Season1Id, EpisodeNumber = 1 },
            new SerieEpisode { Id = Episode2Id, SerieId = SerieId, SeasonId = Season1Id, EpisodeNumber = 2 },
            new SerieEpisode { Id = OtherSeasonEpisodeId, SerieId = SerieId, SeasonId = Season2Id, EpisodeNumber = 1 },
            new MusicArtist { Id = ArtistId, Title = "Artist" },
            new MusicArtist { Id = OtherArtistId, Title = "Other artist" },
            new MusicAlbum { Id = AlbumId, Title = "Album", ArtistId = ArtistId },
            new MusicAlbum { Id = OtherArtistAlbumId, Title = "Other album", ArtistId = OtherArtistId },
            new MusicTrack { Id = TrackId, Title = "Track", AlbumId = AlbumId, ArtistId = ArtistId },
            new MusicTrack { Id = GuestTrackId, Title = "Guest track", AlbumId = OtherArtistAlbumId, ArtistId = ArtistId },
            new Movie { Id = MovieId, Title = "Movie" });

        Guid[] mediaIds =
        [
            SerieId, Season1Id, Season2Id, Episode1Id, Episode2Id, OtherSeasonEpisodeId,
            ArtistId, OtherArtistId, AlbumId, OtherArtistAlbumId, TrackId, GuestTrackId, MovieId
        ];
        Context.MediaLibraryAvailabilities.AddRange(
            mediaIds.Select(id => new MediaLibraryAvailability { MediaId = id, LibraryId = LibraryId }));

        Context.IndexedFiles.AddRange(
            CreateFile(MovieFileId, "movie", MovieId),
            CreateFile(Episode1FileId, "episode", Episode1Id),
            CreateFile(OrphanFileId, "orphan", mediaId: null));

        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    private IndexedFile CreateFile(Guid id, string name, Guid? mediaId) => new()
    {
        Id = id,
        LibraryId = LibraryId,
        Name = name,
        Extension = ".mkv",
        Path = $"/library/{name}.mkv",
        Hash = 1,
        Size = 1024,
        MediaId = mediaId
    };
}

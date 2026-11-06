using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Medias.Queries.GetMedia;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Features.Medias.Queries;

[TestFixture]
public class GetMediaQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private GetMediaQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _episodeId;

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

        _userId = Guid.NewGuid();
        _episodeId = Guid.NewGuid();
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });
        var serie = new Serie { Id = serieId, Title = "Show", SortTitle = "Show" };
        var season = new SerieSeason
        {
            Id = seasonId,
            SerieId = serieId,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };
        serie.Seasons.Add(season);
        var episode = new SerieEpisode
        {
            Id = _episodeId,
            SerieId = serieId,
            Serie = serie,
            SeasonId = seasonId,
            Season = season,
            EpisodeNumber = 1,
            Title = "E1",
            SortTitle = "E1"
        };
        season.Episodes.Add(episode);

        _context.Medias.AddRange(serie, season, episode);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _episodeId,
            IsCompleted = false,
            LastInteractedAt = DateTime.UtcNow
        });
        _context.PlaybackBookmarks.Add(new ItemPlaybackBookmark
        {
            UserId = _userId,
            MediaId = _episodeId,
            PositionSeconds = 333,
            DurationSeconds = 1800,
            UpdatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        var currentUser = Substitute.For<IUser>();
        currentUser.Id.Returns(_userId);
        currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var accessGuard = Substitute.For<IMediaAccessGuard>();
        var bookmarkService = new PlaybackBookmarkService(
            _context,
            NullLogger<PlaybackBookmarkService>.Instance);

        _handler = new GetMediaQueryHandler(_context, currentUser, accessGuard, bookmarkService);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnItemBookmark_SoResumePositionIsMapped()
    {
        var result = await _handler.Handle(new GetMediaQuery(_episodeId), CancellationToken.None);

        result.ItemBookmarks.Should().ContainKey(_episodeId);
        result.ItemBookmarks[_episodeId].PositionSeconds.Should().Be(333);

        var dto = (SerieEpisodeDto)result.Media.ToMediaDto(result.ItemBookmarks);
        dto.UserState!.LastPlaybackPosition.Should().Be(333);
    }

    [Test]
    public async Task Handle_ShouldOmitTracks_WhenLibraryIsExcluded()
    {
        var albumId = Guid.NewGuid();
        var visibleTrackId = Guid.NewGuid();
        var hiddenTrackId = Guid.NewGuid();
        var visibleLibraryId = AddLibrary("Family");
        var hiddenLibraryId = AddLibrary("Private");
        var album = new MusicAlbum { Id = albumId, Title = "Album", SortTitle = "Album" };
        var visible = new MusicTrack
        {
            Id = visibleTrackId,
            Title = "Visible",
            SortTitle = "Visible",
            AlbumId = albumId
        };
        var hidden = new MusicTrack
        {
            Id = hiddenTrackId,
            Title = "PRIVATE-MARKER",
            SortTitle = "PRIVATE-MARKER",
            AlbumId = albumId
        };
        album.Tracks.Add(visible);
        album.Tracks.Add(hidden);
        _context.Medias.AddRange(album, visible, hidden);
        _context.IndexedFiles.AddRange(
            CreateFile(visibleLibraryId, visibleTrackId, "/visible.mp3"),
            CreateFile(hiddenLibraryId, hiddenTrackId, "/hidden.mp3"));
        _context.UserLibraryExclusions.Add(new UserLibraryExclusion
        {
            UserId = _userId,
            LibraryId = hiddenLibraryId,
            IsAdminExcluded = true
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMediaQuery(albumId), CancellationToken.None);

        var loaded = (MusicAlbum)result.Media;
        loaded.Tracks.Select(t => t.Id).Should().Equal(visibleTrackId);
    }

    [Test]
    public async Task Handle_ShouldOmitEpisodes_WhenLibraryIsExcluded()
    {
        var season = await _context.Medias.OfType<SerieSeason>().AsNoTracking()
            .Select(s => new { s.Id, s.SerieId })
            .SingleAsync();
        var keptId = Guid.NewGuid();
        var hiddenId = Guid.NewGuid();
        var visibleLibraryId = AddLibrary("Family");
        var hiddenLibraryId = AddLibrary("Private");
        _context.Medias.AddRange(
            new SerieEpisode
            {
                Id = keptId,
                SerieId = season.SerieId,
                SeasonId = season.Id,
                EpisodeNumber = 2,
                Title = "E2",
                SortTitle = "E2"
            },
            new SerieEpisode
            {
                Id = hiddenId,
                SerieId = season.SerieId,
                SeasonId = season.Id,
                EpisodeNumber = 3,
                Title = "Hidden",
                SortTitle = "Hidden"
            });
        _context.IndexedFiles.AddRange(
            CreateFile(visibleLibraryId, keptId, "/kept.mkv"),
            CreateFile(hiddenLibraryId, hiddenId, "/hidden.mkv"));
        _context.UserLibraryExclusions.Add(new UserLibraryExclusion
        {
            UserId = _userId,
            LibraryId = hiddenLibraryId,
            IsAdminExcluded = true
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMediaQuery(season.Id), CancellationToken.None);

        var loaded = (SerieSeason)result.Media;
        loaded.Episodes.Select(e => e.Id).Should().BeEquivalentTo([_episodeId, keptId]);
    }

    [Test]
    public async Task Handle_ShouldOmitSelfExcludedEpisode_WhenLoadingSeason()
    {
        var season = _context.Medias.OfType<SerieSeason>().AsNoTracking()
            .Select(s => new { s.Id, s.SerieId })
            .Single();
        var keptId = Guid.NewGuid();
        _context.Medias.Add(new SerieEpisode
        {
            Id = keptId,
            SerieId = season.SerieId,
            SeasonId = season.Id,
            EpisodeNumber = 2,
            Title = "E2",
            SortTitle = "E2"
        });
        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = _episodeId,
            IsSelfExcluded = true
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMediaQuery(season.Id), CancellationToken.None);

        var loaded = (SerieSeason)result.Media;
        loaded.Episodes.Select(e => e.Id).Should().Equal(keptId);
    }

    [Test]
    public async Task Handle_ShouldMapTrackUserRating_WhenLoadingAlbum()
    {
        var albumId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var album = new MusicAlbum
        {
            Id = albumId,
            Title = "Album",
            SortTitle = "Album"
        };
        var track = new MusicTrack
        {
            Id = trackId,
            AlbumId = albumId,
            Album = album,
            Title = "Track",
            SortTitle = "Track",
            TrackNumber = 1,
            DiscNumber = 1
        };
        album.Tracks.Add(track);
        _context.Medias.AddRange(album, track);
        _context.Ratings.Add(new UserRating
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = trackId,
            Value = 8,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMediaQuery(albumId), CancellationToken.None);
        var dto = (MusicAlbumDto)result.Media.ToMediaDto(result.ItemBookmarks);

        dto.Tracks.Should().ContainSingle();
        dto.Tracks![0].UserRating.Should().Be(8);
    }

    [Test]
    public async Task Handle_ShouldMapAlbumUserRatings_WhenLoadingArtist()
    {
        var artistId = Guid.NewGuid();
        var albumId = Guid.NewGuid();
        var shellAlbumId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var artist = new MusicArtist
        {
            Id = artistId,
            Title = "Artist",
            SortTitle = "Artist"
        };
        var album = new MusicAlbum
        {
            Id = albumId,
            Title = "Album",
            SortTitle = "Album",
            ArtistId = artistId,
            Artist = artist
        };
        var shell = new MusicAlbum
        {
            Id = shellAlbumId,
            Title = "Album (imported)",
            SortTitle = "Album (imported)",
            ArtistId = artistId,
            Artist = artist
        };
        var track = new MusicTrack
        {
            Id = trackId,
            AlbumId = albumId,
            Title = "Track",
            SortTitle = "Track"
        };
        artist.Albums.Add(album);
        artist.Albums.Add(shell);
        album.Tracks.Add(track);

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
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            MediaId = trackId,
            Name = "track",
            Extension = ".flac",
            Path = "/music/album/track.flac",
            Hash = 1,
            Size = 1
        });
        _context.Medias.AddRange(artist, album, shell, track);
        _context.Ratings.Add(new UserRating
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = albumId,
            Value = 7,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMediaQuery(artistId), CancellationToken.None);
        var dto = (MusicArtistDto)result.Media.ToMediaDto(result.ItemBookmarks);

        dto.Albums.Should().ContainSingle();
        dto.Albums![0].Id.Should().Be(albumId);
        GetUserRating(dto.Albums[0].Ratings).Should().Be(7);
    }

    private static int? GetUserRating(IReadOnlyList<RatingDto>? ratings) =>
        ratings?.FirstOrDefault(r => r.Source == RatingSource.LocalUser)?.Value is double value
            ? (int)value
            : null;

    private Guid AddLibrary(string title)
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = title,
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Music,
            Title = title,
            MetadataProviderName = "none",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        return libraryId;
    }

    private static IndexedFile CreateFile(Guid libraryId, Guid mediaId, string path) =>
        new()
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            Name = Path.GetFileNameWithoutExtension(path),
            Extension = Path.GetExtension(path),
            Path = path,
            Hash = 7,
            Size = 1000,
            MediaId = mediaId
        };
}

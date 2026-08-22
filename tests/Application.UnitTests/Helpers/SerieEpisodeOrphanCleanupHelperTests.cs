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
public class SerieEpisodeOrphanCleanupHelperTests
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
    public async Task TryDeleteIfOrphanAsync_ShouldDeleteEpisodeAndEmptySeason_WhenNoFilesAndNoUserData()
    {
        var (episodeId, seasonId, serieId) = await SeedOrphanEpisodeAsync();

        var deleted = await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
            _context,
            episodeId,
            _logger);
        await _context.SaveChangesAsync();

        deleted.Should().BeTrue();
        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == episodeId)).Should().BeFalse();
        (await _context.Medias.OfType<SerieSeason>().AnyAsync(s => s.Id == seasonId)).Should().BeFalse();
        (await _context.Medias.OfType<Serie>().AnyAsync(s => s.Id == serieId)).Should().BeFalse();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepSerie_WhenSerieHasUserData()
    {
        var (episodeId, seasonId, serieId) = await SeedOrphanEpisodeAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = serieId,
            PlayCount = 1
        });
        await _context.SaveChangesAsync();

        var deleted = await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
            _context,
            episodeId,
            _logger);
        await _context.SaveChangesAsync();

        deleted.Should().BeTrue();
        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == episodeId)).Should().BeFalse();
        (await _context.Medias.OfType<SerieSeason>().AnyAsync(s => s.Id == seasonId)).Should().BeFalse();
        (await _context.Medias.OfType<Serie>().AnyAsync(s => s.Id == serieId)).Should().BeTrue();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepEpisode_WhenUserMediaStateExists()
    {
        var (episodeId, _, _) = await SeedOrphanEpisodeAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = episodeId,
            PlayCount = 1,
        });
        await _context.SaveChangesAsync();

        var deleted = await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
            _context,
            episodeId,
            _logger);

        deleted.Should().BeFalse();
        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == episodeId)).Should().BeTrue();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepEpisode_WhenIndexedFileRemains()
    {
        var (episodeId, _, _) = await SeedOrphanEpisodeAsync(withFile: true);

        var deleted = await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
            _context,
            episodeId,
            _logger);

        deleted.Should().BeFalse();
        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == episodeId)).Should().BeTrue();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepEpisode_WhenCollectionItemExists()
    {
        var (episodeId, _, _) = await SeedOrphanEpisodeAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        var collection = new Collection { Id = Guid.NewGuid(), Title = "Favs", UserId = userId };
        _context.Collections.Add(collection);
        _context.CollectionItems.Add(new CollectionItem
        {
            CollectionId = collection.Id,
            MediaId = episodeId,
            Order = 0
        });
        await _context.SaveChangesAsync();

        var deleted = await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
            _context,
            episodeId,
            _logger);

        deleted.Should().BeFalse();
        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == episodeId)).Should().BeTrue();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepEpisode_WhenPlaybackSessionExists()
    {
        var (episodeId, _, _) = await SeedOrphanEpisodeAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            UserId = userId,
            MediaId = episodeId,
            SessionId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            PositionSeconds = 1,
            DurationSeconds = 10,
            WatchedDurationSeconds = 1,
            State = PlaybackState.Ended
        });
        await _context.SaveChangesAsync();

        var deleted = await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
            _context,
            episodeId,
            _logger);

        deleted.Should().BeFalse();
        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == episodeId)).Should().BeTrue();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepEpisode_WhenExclusionExists()
    {
        var (episodeId, _, _) = await SeedOrphanEpisodeAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = userId,
            MediaId = episodeId,
            IsSelfExcluded = true
        });
        await _context.SaveChangesAsync();

        var deleted = await SerieEpisodeOrphanCleanupHelper.TryDeleteIfOrphanAsync(
            _context,
            episodeId,
            _logger);

        deleted.Should().BeFalse();
        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == episodeId)).Should().BeTrue();
    }

    private async Task<(Guid EpisodeId, Guid SeasonId, Guid SerieId)> SeedOrphanEpisodeAsync(bool withFile = false)
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie,
            RootPath = "/media/series",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

        var serie = new Serie { Id = Guid.NewGuid(), Title = "Show", SortTitle = "Show" };
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 0,
            Title = "Specials",
            SortTitle = "Specials"
        };
        serie.Seasons.Add(season);

        var episode = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 99,
            Title = "Episode 99",
            SortTitle = "Episode 99",
            IndexedFiles = []
        };
        season.Episodes.Add(episode);

        _context.Medias.Add(serie);
        _context.Medias.Add(season);
        _context.Medias.Add(episode);

        if (withFile)
        {
            var file = new IndexedFile
            {
                Id = Guid.NewGuid(),
                LibraryId = libraryId,
                Name = "Show - S00E099.mkv",
                Extension = ".mkv",
                Path = "/media/series/Show/Specials/Show - S00E099.mkv",
                ParentDirectory = "Specials",
                Hash = 1,
                Size = 1,
                MediaId = episode.Id
            };
            episode.IndexedFiles.Add(file);
            _context.IndexedFiles.Add(file);
        }

        await _context.SaveChangesAsync();
        return (episode.Id, season.Id, serie.Id);
    }
}

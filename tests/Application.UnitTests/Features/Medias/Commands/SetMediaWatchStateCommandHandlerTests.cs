using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Commands.SetMediaWatchState;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Enums;
using K7.Tests.Helpers.Samples;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class SetMediaWatchStateCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private SetMediaWatchStateCommandHandler _handler = null!;
    private Guid _userId;
    private Guid _serieId;
    private Guid _seasonId;
    private Guid _episode1Id;
    private Guid _episode2Id;

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
        _context.Users.Add(new User { Id = _userId, IdentityUserId = "ident", DisplayName = "viewer" });

        var serie = new Serie { Id = Guid.NewGuid(), Title = "Show", SortTitle = "Show" };
        _serieId = serie.Id;
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };
        _seasonId = season.Id;
        serie.Seasons.Add(season);

        _episode1Id = Guid.NewGuid();
        _episode2Id = Guid.NewGuid();
        var episode1 = CreateEpisode(_episode1Id, serie, season, 1);
        var episode2 = CreateEpisode(_episode2Id, serie, season, 2);

        _context.Medias.AddRange(serie, season, episode1, episode2);
        var (libraryId, peerServerId) = RemoteIndexedFilesSamples.EnsureLibraryAndPeer(_context);
        _context.RemoteIndexedFiles.AddRange(
            RemoteIndexedFilesSamples.Create(_episode1Id, libraryId, peerServerId),
            RemoteIndexedFilesSamples.Create(_episode2Id, libraryId, peerServerId));
        _context.SaveChanges();

        var currentUser = Substitute.For<IUser>();
        currentUser.Id.Returns(_userId);
        currentUser.IdentityId.Returns("ident");

        _handler = new SetMediaWatchStateCommandHandler(
            _context,
            currentUser,
            Substitute.For<IMediaAccessGuard>(),
            new PlaybackBookmarkService(_context, NullLogger<PlaybackBookmarkService>.Instance),
            Substitute.For<IPlaybackProgressNotifier>(),
            Substitute.For<IMediaQueryCacheInvalidator>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldRemoveSeriesBookmark_WhenSerieMarkedUnwatched()
    {
        var bookmarkService = new PlaybackBookmarkService(_context, NullLogger<PlaybackBookmarkService>.Instance);
        await bookmarkService.OnEpisodeCompletedAsync(_userId, null, _episode1Id, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new SetMediaWatchStateCommand(_serieId, Watched: false, WatchStateScope.Serie),
            CancellationToken.None);

        (await _context.PlaybackBookmarks.OfType<SeriesPlaybackBookmark>().CountAsync()).Should().Be(0);
        (await _context.UserMediaStates.CountAsync(s => s.IsCompleted)).Should().Be(0);
    }

    [Test]
    public async Task Handle_ShouldRemoveSeriesBookmark_WhenSeasonMarkedUnwatched()
    {
        var bookmarkService = new PlaybackBookmarkService(_context, NullLogger<PlaybackBookmarkService>.Instance);
        await bookmarkService.OnEpisodeCompletedAsync(_userId, null, _episode1Id, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new SetMediaWatchStateCommand(_seasonId, Watched: false, WatchStateScope.Season),
            CancellationToken.None);

        (await _context.PlaybackBookmarks.OfType<SeriesPlaybackBookmark>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_ShouldLeaveDormantSeriesBookmark_WhenSerieMarkedWatched()
    {
        var bookmarkService = new PlaybackBookmarkService(_context, NullLogger<PlaybackBookmarkService>.Instance);
        await bookmarkService.OnEpisodeCompletedAsync(_userId, null, _episode1Id, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new SetMediaWatchStateCommand(_serieId, Watched: true, WatchStateScope.Serie),
            CancellationToken.None);

        var bookmark = await _context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .SingleAsync(b => b.UserId == _userId && b.SerieId == _serieId);

        bookmark.LastCompletedEpisodeId.Should().Be(_episode2Id);
        bookmark.NextEpisodeId.Should().BeNull();
        (await _context.UserMediaStates.CountAsync(s => s.IsCompleted)).Should().Be(2);
    }

    [Test]
    public async Task Handle_ShouldRemoveItemBookmark_WhenInProgressMovieMarkedUnwatched()
    {
        var movieId = Guid.NewGuid();
        _context.Medias.Add(new Movie { Id = movieId, Title = "Film", SortTitle = "Film" });
        var bookmarkService = new PlaybackBookmarkService(_context, NullLogger<PlaybackBookmarkService>.Instance);
        await bookmarkService.UpsertItemBookmarkAsync(_userId, null, movieId, 600, 3600, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new SetMediaWatchStateCommand(movieId, Watched: false, WatchStateScope.Item),
            CancellationToken.None);

        (await _context.PlaybackBookmarks.CountAsync()).Should().Be(0);
        (await _context.UserMediaStates.SingleAsync(s => s.MediaId == movieId)).IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldRemoveItemBookmark_WhenCompletedMovieMarkedUnwatched()
    {
        var movieId = Guid.NewGuid();
        _context.Medias.Add(new Movie { Id = movieId, Title = "Film", SortTitle = "Film" });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = movieId,
            IsCompleted = true,
            LastInteractedAt = DateTime.UtcNow
        });
        var bookmarkService = new PlaybackBookmarkService(_context, NullLogger<PlaybackBookmarkService>.Instance);
        await bookmarkService.UpsertItemBookmarkAsync(_userId, null, movieId, 3500, 3600, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new SetMediaWatchStateCommand(movieId, Watched: false, WatchStateScope.Item),
            CancellationToken.None);

        (await _context.PlaybackBookmarks.CountAsync()).Should().Be(0);
        (await _context.UserMediaStates.SingleAsync(s => s.MediaId == movieId)).IsCompleted.Should().BeFalse();
    }

    private static SerieEpisode CreateEpisode(Guid id, Serie serie, SerieSeason season, int number) =>
        new()
        {
            Id = id,
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = number,
            Title = $"E{number}",
            SortTitle = $"E{number}"
        };
}

using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Users.Commands.BulkUpsertMediaStates;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Requests;
using K7.Tests.Helpers.Samples;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.Users.Commands;

[TestFixture]
public class BulkUpsertMediaStatesCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private BulkUpsertMediaStatesCommandHandler _handler = null!;
    private Guid _userId;
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
        _context.Users.Add(new User { Id = _userId, IdentityUserId = "u1", DisplayName = "user" });

        var serie = new Serie { Id = Guid.NewGuid(), Title = "Show", SortTitle = "Show" };
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };
        serie.Seasons.Add(season);

        _episode1Id = Guid.NewGuid();
        _episode2Id = Guid.NewGuid();
        var episode1 = new SerieEpisode
        {
            Id = _episode1Id,
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 1,
            Title = "E1",
            SortTitle = "E1"
        };
        var episode2 = new SerieEpisode
        {
            Id = _episode2Id,
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 2,
            Title = "E2",
            SortTitle = "E2"
        };

        _context.Medias.AddRange(serie, season, episode1, episode2);
        var (libraryId, peerServerId) = RemoteIndexedFilesSamples.EnsureLibraryAndPeer(_context);
        _context.RemoteIndexedFiles.Add(RemoteIndexedFilesSamples.Create(_episode2Id, libraryId, peerServerId));
        _context.SaveChanges();

        var cacheInvalidator = Substitute.For<IMediaQueryCacheInvalidator>();
        _handler = new BulkUpsertMediaStatesCommandHandler(
            _context,
            cacheInvalidator,
            new PlaybackBookmarkService(_context, Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaybackBookmarkService>.Instance));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldCreateSeriesBookmark_WhenImportedEpisodeIsCompleted()
    {
        var interactedAt = DateTime.UtcNow.AddDays(-2);
        var count = await _handler.Handle(new BulkUpsertMediaStatesCommand
        {
            UserId = _userId,
            Items =
            [
                new BulkUpsertMediaStatesRequest.MediaStateItem
                {
                    MediaId = _episode1Id,
                    PlayCount = 1,
                    IsCompleted = true,
                    LastInteractedAt = interactedAt
                }
            ]
        }, CancellationToken.None);

        count.Should().Be(1);

        var seriesBookmark = await _context.PlaybackBookmarks
            .OfType<SeriesPlaybackBookmark>()
            .SingleAsync(b => b.UserId == _userId);
        seriesBookmark.LastCompletedEpisodeId.Should().Be(_episode1Id);
        seriesBookmark.NextEpisodeId.Should().Be(_episode2Id);
        seriesBookmark.ActivityAt.Should().Be(interactedAt);
    }
}

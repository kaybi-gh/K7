using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Queries.GetNextEpisode;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Medias.Queries;

[TestFixture]
public class GetNextEpisodeQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IPlaybackBookmarkService _bookmarks = null!;
    private GetNextEpisodeQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _libraryId;
    private Guid _serieId;
    private Guid _season1Id;

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
        _libraryId = Guid.NewGuid();
        _serieId = Guid.NewGuid();
        _season1Id = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });
        _context.LibraryGroups.Add(new LibraryGroup { Id = groupId, Title = "Series", MediaType = LibraryMediaType.Serie });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Serie,
            Title = "Series",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.Medias.Add(new Serie { Id = _serieId, Title = "Show" });
        _context.Medias.Add(new SerieSeason { Id = _season1Id, Title = "S1", SerieId = _serieId, SeasonNumber = 1 });
        _context.SaveChanges();

        var currentUser = Substitute.For<IUser>();
        currentUser.Id.Returns(_userId);
        currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        _bookmarks = Substitute.For<IPlaybackBookmarkService>();
        _bookmarks.GetItemBookmarksAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, ItemPlaybackBookmark>());

        _handler = new GetNextEpisodeQueryHandler(_context, currentUser, Substitute.For<IMediaAccessGuard>(), _bookmarks);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnInProgressEpisodeWithItsPosition_WhenNoCurrentEpisodeIsGiven()
    {
        var first = AddEpisode(1, withFile: true);
        var started = AddEpisode(2, withFile: true);
        AddState(first, completed: true);
        AddState(started, completed: false);
        await _context.SaveChangesAsync();

        _bookmarks.GetItemBookmarksAsync(_userId, null, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, ItemPlaybackBookmark>
            {
                [started] = new() { MediaId = started, UserId = _userId, PositionSeconds = 600, DurationSeconds = 2400 }
            });

        var result = await _handler.Handle(new GetNextEpisodeQuery(_serieId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(started);
        result.UserState!.LastPlaybackPosition.Should().Be(600);
        result.UserState.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldSkipEpisodesWithoutFile_WhenPickingTheNextUnwatched()
    {
        var watched = AddEpisode(1, withFile: true);
        AddEpisode(2, withFile: false);
        var playable = AddEpisode(3, withFile: true);
        AddState(watched, completed: true);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetNextEpisodeQuery(_serieId), CancellationToken.None);

        result!.Id.Should().Be(playable);
    }

    [Test]
    public async Task Handle_ShouldSkipExcludedEpisode_WhenPickingTheNextUnwatched()
    {
        var hidden = AddEpisode(1, withFile: true);
        var visible = AddEpisode(2, withFile: true);
        _context.UserMediaExclusions.Add(new UserMediaExclusion { UserId = _userId, MediaId = hidden, IsSelfExcluded = true });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetNextEpisodeQuery(_serieId), CancellationToken.None);

        result!.Id.Should().Be(visible);
    }

    [Test]
    public async Task Handle_ShouldReturnNull_WhenNoEpisodeIsPlayable()
    {
        AddEpisode(1, withFile: false);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetNextEpisodeQuery(_serieId), CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldReturnTheFollowingEpisode_WhenCurrentEpisodeIsGiven()
    {
        var current = AddEpisode(1, withFile: true);
        var following = AddEpisode(2, withFile: true);
        AddState(following, completed: true);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetNextEpisodeQuery(_serieId, current), CancellationToken.None);

        result!.Id.Should().Be(following);
    }

    private Guid AddEpisode(int number, bool withFile)
    {
        var id = Guid.NewGuid();
        _context.Medias.Add(new SerieEpisode
        {
            Id = id,
            Title = $"E{number}",
            SerieId = _serieId,
            SeasonId = _season1Id,
            EpisodeNumber = number
        });

        if (withFile)
        {
            _context.IndexedFiles.Add(new IndexedFile
            {
                Id = Guid.NewGuid(),
                LibraryId = _libraryId,
                Name = $"e{number}",
                Extension = ".mkv",
                Path = $"/series/show/e{number}.mkv",
                ParentDirectory = "show",
                Hash = 42,
                Size = 1000,
                MediaId = id
            });
        }

        return id;
    }

    private void AddState(Guid mediaId, bool completed) =>
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = mediaId,
            IsCompleted = completed,
            LastInteractedAt = DateTime.UtcNow
        });
}

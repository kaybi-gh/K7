using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Music.Queries.GetMusicHitParade;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Music.Queries;

[TestFixture]
public class GetMusicHitParadeQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private GetMusicHitParadeQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _libraryId;
    private Guid _albumId;
    private Guid _trackAId;
    private Guid _trackBId;

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
        _albumId = Guid.NewGuid();
        _trackAId = Guid.NewGuid();
        _trackBId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, IdentityUserId = "ident", DisplayName = "listener" });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Music",
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Music,
            Title = "Music",
            MetadataProviderName = "none",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.Medias.Add(new MusicAlbum { Id = _albumId, Title = "Album" });
        _context.Medias.Add(new MusicTrack { Id = _trackAId, Title = "Track A", AlbumId = _albumId });
        _context.Medias.Add(new MusicTrack { Id = _trackBId, Title = "Track B", AlbumId = _albumId });
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = _trackAId, LibraryId = _libraryId },
            new MediaLibraryAvailability { MediaId = _trackBId, LibraryId = _libraryId });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        _handler = new GetMusicHitParadeQueryHandler(
            _context,
            _currentUser,
            new LiteMediaProjectionService(_context),
            new MediaAccessFilter(_context));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldIgnoreSessionsWithoutCompletedAt()
    {
        AddSession(_trackAId, _userId, completed: false, startedAt: Utc(2026, 3, 1));
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 3, 2));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicHitParadeQuery(), CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackAId && t.PlayCount == 1);
    }

    [Test]
    public async Task Handle_ShouldDeduplicateReferenceId()
    {
        var referenceId = Guid.NewGuid();
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 4, 1), referenceId: referenceId);
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 4, 1, 1), referenceId: referenceId);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicHitParadeQuery(), CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackAId && t.PlayCount == 1);
    }

    [Test]
    public async Task Handle_ShouldFilterByPeriodBounds()
    {
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2025, 6, 1));
        AddSession(_trackBId, _userId, completed: true, startedAt: Utc(2026, 2, 1));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetMusicHitParadeQuery
            {
                Period = MusicHitParadePeriods.Year,
                Year = 2026,
                From = Utc(2026, 1, 1),
                To = Utc(2027, 1, 1)
            },
            CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackBId);
    }

    [Test]
    public async Task Handle_ShouldExcludeOtherUsers_WhenScopeIsPersonal()
    {
        var otherUserId = Guid.NewGuid();
        _context.Users.Add(new User { Id = otherUserId, DisplayName = "other" });
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 1, 1));
        AddSession(_trackBId, otherUserId, completed: true, startedAt: Utc(2026, 1, 2));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetMusicHitParadeQuery { Scope = MusicHitParadeScopes.Personal },
            CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackAId);
    }

    [Test]
    public async Task Handle_ShouldIncludeAllUsers_WhenScopeIsServer()
    {
        var otherUserId = Guid.NewGuid();
        _context.Users.Add(new User { Id = otherUserId, DisplayName = "other" });
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 1, 1));
        AddSession(_trackBId, otherUserId, completed: true, startedAt: Utc(2026, 1, 2));
        AddSession(_trackBId, otherUserId, completed: true, startedAt: Utc(2026, 1, 3));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetMusicHitParadeQuery { Scope = MusicHitParadeScopes.Server },
            CancellationToken.None);

        result.Tracks.Select(t => t.Track.Id).Should().Equal(_trackBId, _trackAId);
        result.Tracks[0].PlayCount.Should().Be(2);
        result.Tracks[1].PlayCount.Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldIncludeCoViewerSessions_WhenScopeIsPersonal()
    {
        var actorUserId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        _context.Users.Add(new User { Id = actorUserId, DisplayName = "actor" });
        AddSession(_trackAId, actorUserId, completed: true, startedAt: Utc(2026, 5, 1), referenceId: referenceId);
        _context.MediaPlaybackSessionCoViewers.Add(new MediaPlaybackSessionCoViewer
        {
            ReferenceId = referenceId,
            UserId = _userId
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicHitParadeQuery(), CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackAId && t.PlayCount == 1);
    }

    [Test]
    public async Task Handle_ShouldIgnoreNonMusicSessions()
    {
        var movieId = Guid.NewGuid();
        _context.Medias.Add(new Movie { Id = movieId, Title = "Film" });
        AddSession(movieId, _userId, completed: true, startedAt: Utc(2026, 1, 1));
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 1, 2));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicHitParadeQuery(), CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackAId);
    }

    [Test]
    public async Task Handle_ShouldExcludeTracks_WhenLibraryIsExcluded()
    {
        var excludedLibraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Hidden music",
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = excludedLibraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Music,
            Title = "Hidden",
            MetadataProviderName = "none",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.MediaLibraryAvailabilities.RemoveRange(
            _context.MediaLibraryAvailabilities.Where(a => a.MediaId == _trackBId));
        _context.MediaLibraryAvailabilities.Add(
            new MediaLibraryAvailability { MediaId = _trackBId, LibraryId = excludedLibraryId });
        _context.UserLibraryExclusions.Add(new UserLibraryExclusion
        {
            UserId = _userId,
            LibraryId = excludedLibraryId,
            IsAdminExcluded = true
        });
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 1, 1));
        AddSession(_trackBId, _userId, completed: true, startedAt: Utc(2026, 1, 2));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetMusicHitParadeQuery { Scope = MusicHitParadeScopes.Server },
            CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackAId);
    }

    [Test]
    public async Task Handle_ShouldExcludeTracks_WhenMediaIsExcluded()
    {
        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = _userId,
            MediaId = _trackBId,
            IsSelfExcluded = true
        });
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 1, 1));
        AddSession(_trackBId, _userId, completed: true, startedAt: Utc(2026, 1, 2));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicHitParadeQuery(), CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackAId);
    }

    [Test]
    public async Task Handle_ShouldExcludeTracks_WhenLibraryAvailabilityIsMissing()
    {
        var hiddenTrackId = Guid.NewGuid();
        _context.Medias.Add(new MusicTrack { Id = hiddenTrackId, Title = "Hidden", AlbumId = _albumId });
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 1, 1));
        AddSession(hiddenTrackId, _userId, completed: true, startedAt: Utc(2026, 1, 2));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetMusicHitParadeQuery(), CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackAId);
    }

    [Test]
    public async Task Handle_ShouldScopeToSharedProfileOnly_WhenSharedProfileActive()
    {
        var sharedProfileId = Guid.NewGuid();
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Couple",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 1, 1), sharedProfileId: sharedProfileId);
        AddSession(_trackBId, _userId, completed: true, startedAt: Utc(2026, 1, 2));
        await _context.SaveChangesAsync();

        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(sharedProfileId);

        var result = await _handler.Handle(new GetMusicHitParadeQuery(), CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackAId && t.PlayCount == 1);
    }

    [Test]
    public async Task Handle_ShouldFilterByCustomFromTo()
    {
        AddSession(_trackAId, _userId, completed: true, startedAt: Utc(2026, 1, 1));
        AddSession(_trackBId, _userId, completed: true, startedAt: Utc(2026, 3, 1));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetMusicHitParadeQuery
            {
                Period = MusicHitParadePeriods.Custom,
                From = Utc(2026, 2, 1),
                To = Utc(2026, 4, 1)
            },
            CancellationToken.None);

        result.Tracks.Should().ContainSingle(t => t.Track.Id == _trackBId);
        result.From.Should().Be(Utc(2026, 2, 1));
        result.To.Should().Be(Utc(2026, 4, 1));
    }

    private void AddSession(
        Guid mediaId,
        Guid userId,
        bool completed,
        DateTime startedAt,
        Guid? referenceId = null,
        Guid? sharedProfileId = null)
    {
        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MediaId = mediaId,
            SessionId = Guid.NewGuid(),
            ReferenceId = referenceId ?? Guid.NewGuid(),
            SharedProfileId = sharedProfileId,
            StartedAt = startedAt,
            CompletedAt = completed ? startedAt.AddMinutes(3) : null,
            DurationSeconds = 180,
            WatchedDurationSeconds = completed ? 180 : 5,
            State = PlaybackState.Ended
        });
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);
}

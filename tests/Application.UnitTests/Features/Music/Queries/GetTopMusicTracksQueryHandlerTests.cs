using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Music.Queries.GetTopMusicTracks;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Music.Queries;

[TestFixture]
public class GetTopMusicTracksQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private GetTopMusicTracksQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _libraryId;
    private Guid _albumId;
    private Guid _visibleTrackId;
    private Guid _hiddenTrackId;

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
        _visibleTrackId = Guid.NewGuid();
        _hiddenTrackId = Guid.NewGuid();
        var hiddenLibraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var hiddenGroupId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _context.Users.AddRange(
            new User { Id = _userId, IdentityUserId = "ident", DisplayName = "listener" },
            new User { Id = otherUserId, IdentityUserId = "other", DisplayName = "other" });
        _context.LibraryGroups.AddRange(
            new LibraryGroup { Id = groupId, Title = "Music", MediaType = LibraryMediaType.Music },
            new LibraryGroup { Id = hiddenGroupId, Title = "Hidden", MediaType = LibraryMediaType.Music });
        _context.Libraries.AddRange(
            new Library
            {
                Id = _libraryId,
                LibraryGroupId = groupId,
                MediaType = LibraryMediaType.Music,
                Title = "Music",
                MetadataProviderName = "none",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            },
            new Library
            {
                Id = hiddenLibraryId,
                LibraryGroupId = hiddenGroupId,
                MediaType = LibraryMediaType.Music,
                Title = "Hidden",
                MetadataProviderName = "none",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            });
        _context.Medias.Add(new MusicAlbum { Id = _albumId, Title = "Album" });
        _context.Medias.Add(new MusicTrack { Id = _visibleTrackId, Title = "Visible", AlbumId = _albumId });
        _context.Medias.Add(new MusicTrack { Id = _hiddenTrackId, Title = "PRIVATE-MARKER", AlbumId = _albumId });
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = _visibleTrackId, LibraryId = _libraryId },
            new MediaLibraryAvailability { MediaId = _hiddenTrackId, LibraryId = hiddenLibraryId });
        _context.UserLibraryExclusions.Add(new UserLibraryExclusion
        {
            UserId = _userId,
            LibraryId = hiddenLibraryId,
            IsAdminExcluded = true
        });
        _context.UserMediaStates.AddRange(
            new UserMediaState { UserId = _userId, MediaId = _visibleTrackId, PlayCount = 1 },
            new UserMediaState { UserId = otherUserId, MediaId = _visibleTrackId, PlayCount = 3 },
            new UserMediaState { UserId = otherUserId, MediaId = _hiddenTrackId, PlayCount = 7 });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        _handler = new GetTopMusicTracksQueryHandler(
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
    public async Task Handle_ShouldExcludeTracks_WhenLibraryIsExcluded()
    {
        var result = await _handler.Handle(new GetTopMusicTracksQuery { Count = 50 }, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Track.Id.Should().Be(_visibleTrackId);
        result[0].PlayCount.Should().Be(4);
    }

    [Test]
    public async Task Handle_ShouldExcludeTracks_WhenMediaIsExcluded()
    {
        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = _userId,
            MediaId = _visibleTrackId,
            IsAdminExcluded = true
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetTopMusicTracksQuery { Count = 50 }, CancellationToken.None);

        result.Should().BeEmpty();
    }
}

using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.MusicRadio.Queries.GetMusicRadio;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.MusicRadio.Queries;

[TestFixture]
public class GetMusicRadioQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IMusicIntelligenceService _musicIntelligence = null!;
    private GetMusicRadioQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _libraryId;
    private Guid _albumId;
    private Guid _favoriteTrackId;
    private Guid _similarTrackId;

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
        _favoriteTrackId = Guid.NewGuid();
        _similarTrackId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var artistId = Guid.NewGuid();

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

        var artist = new MusicArtist { Id = artistId, Title = "Artist", SortTitle = "Artist" };
        var album = new MusicAlbum
        {
            Id = _albumId,
            Title = "Album",
            SortTitle = "Album",
            ArtistId = artistId,
            Artist = artist
        };
        var favorite = CreateTrack(_favoriteTrackId, "Favorite", artistId, album);
        var similar = CreateTrack(_similarTrackId, "Similar", artistId, album);
        _context.Medias.AddRange(artist, album, favorite, similar);
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = _favoriteTrackId, LibraryId = _libraryId },
            new MediaLibraryAvailability { MediaId = _similarTrackId, LibraryId = _libraryId });
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _favoriteTrackId,
            Value = 10,
            MinimumValue = 0,
            MaximumValue = 10
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _musicIntelligence = Substitute.For<IMusicIntelligenceService>();
        _musicIntelligence.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(true);

        _handler = new GetMusicRadioQueryHandler(
            _context,
            _currentUser,
            _musicIntelligence,
            new LiteMediaProjectionService(_context));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_DiscoveryAi_ShouldUseFavoriteTrackAsSimilarSeed()
    {
        _musicIntelligence
            .GetSimilarTracksAsync(
                _favoriteTrackId,
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns([new MusicIntelligenceTrackMatchDto { ItemId = _similarTrackId }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.DiscoveryAi,
            Limit = 3
        }, CancellationToken.None);

        result.Should().ContainSingle(t => t.Id == _similarTrackId);
        await _musicIntelligence.DidNotReceive()
            .GetDiscoveryTracksAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_DiscoveryAi_ShouldReturnSimilarTracks_WhenNeighborsWereAlreadyPlayed()
    {
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _similarTrackId,
            PlayCount = 4,
            LastInteractedAt = DateTime.UtcNow
        });
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _similarTrackId,
            Value = 4,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        _musicIntelligence
            .GetSimilarTracksAsync(
                _favoriteTrackId,
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns([new MusicIntelligenceTrackMatchDto { ItemId = _similarTrackId }]);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.DiscoveryAi,
            Limit = 3
        }, CancellationToken.None);

        result.Should().ContainSingle(t => t.Id == _similarTrackId);
    }

    [Test]
    public async Task Handle_DiscoveryAi_ShouldReturnEmpty_WhenIntelligenceDisabled()
    {
        _musicIntelligence.IsEnabledAsync(Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new GetMusicRadioQuery
        {
            RadioType = MusicRadioType.DiscoveryAi,
            Limit = 3
        }, CancellationToken.None);

        result.Should().BeEmpty();
        await _musicIntelligence.DidNotReceive()
            .GetSimilarTracksAsync(
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }

    private static MusicTrack CreateTrack(Guid trackId, string title, Guid artistId, MusicAlbum album) =>
        new()
        {
            Id = trackId,
            Title = title,
            SortTitle = title,
            ArtistId = artistId,
            Artist = album.Artist,
            AlbumId = album.Id,
            Album = album
        };
}

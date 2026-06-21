using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Queries.GetArtistTopTracks;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.Medias.Queries;

[TestFixture]
public class GetArtistTopTracksQueryHandlerTests
{
    private IApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IMediaAccessGuard _accessGuard = null!;
    private GetArtistTopTracksQueryHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _currentUser = Substitute.For<IUser>();
        _accessGuard = Substitute.For<IMediaAccessGuard>();
        _handler = new GetArtistTopTracksQueryHandler(_context, _currentUser, _accessGuard);
    }

    [Test]
    public async Task Handle_ShouldReturnEmpty_WhenMediaIsNotArtist()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var movie = new Movie { Id = mediaId, Title = "Not An Artist" };
        var medias = new List<BaseMedia> { movie }.BuildMockDbSet();
        _context.Medias.Returns(medias);

        var states = new List<UserMediaState>().BuildMockDbSet();
        _context.UserMediaStates.Returns(states);

        var ratings = new List<BaseRating>().BuildMockDbSet();
        _context.Ratings.Returns(ratings);

        // Act
        var result = await _handler.Handle(new GetArtistTopTracksQuery { ArtistId = mediaId }, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _accessGuard.Received(1).EnsureAccessAsync(mediaId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnTracksOrderedByPlayCount()
    {
        // Arrange
        var artistId = Guid.NewGuid();
        var track1Id = Guid.NewGuid();
        var track2Id = Guid.NewGuid();

        var artist = new MusicArtist { Id = artistId, Title = "Artist" };
        var track1 = new MusicTrack
        {
            Id = track1Id,
            Title = "Track One",
            ArtistId = artistId,
            IndexedFiles = [CreateIndexedFile()]
        };
        var track2 = new MusicTrack
        {
            Id = track2Id,
            Title = "Track Two",
            ArtistId = artistId,
            IndexedFiles = [CreateIndexedFile()]
        };

        var medias = new List<BaseMedia> { artist, track1, track2 }.BuildMockDbSet();
        _context.Medias.Returns(medias);

        var states = new List<UserMediaState>
        {
            new() { Id = Guid.NewGuid(), MediaId = track1Id, PlayCount = 5, UserId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), MediaId = track1Id, PlayCount = 3, UserId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), MediaId = track2Id, PlayCount = 10, UserId = Guid.NewGuid() }
        }.BuildMockDbSet();
        _context.UserMediaStates.Returns(states);

        var ratings = new List<BaseRating>().BuildMockDbSet();
        _context.Ratings.Returns(ratings);

        _currentUser.Id.Returns((Guid?)null);

        // Act
        var result = await _handler.Handle(
            new GetArtistTopTracksQuery { ArtistId = artistId, Count = 10 },
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Track Two");
        result[1].Title.Should().Be("Track One");
    }

    [Test]
    public async Task Handle_ShouldFallbackToCatalog_WhenNoPlayCountsOrRatings()
    {
        // Arrange
        var artistId = Guid.NewGuid();
        var albumId = Guid.NewGuid();
        var track1Id = Guid.NewGuid();
        var track2Id = Guid.NewGuid();

        var artist = new MusicArtist { Id = artistId, Title = "Artist" };
        var album = new MusicAlbum
        {
            Id = albumId,
            Title = "Album",
            ArtistId = artistId,
            ReleaseDate = new DateOnly(2024, 1, 1)
        };
        var track1 = new MusicTrack
        {
            Id = track1Id,
            Title = "Track One",
            ArtistId = artistId,
            AlbumId = albumId,
            TrackNumber = 1,
            IndexedFiles = [CreateIndexedFile()]
        };
        var track2 = new MusicTrack
        {
            Id = track2Id,
            Title = "Track Two",
            ArtistId = artistId,
            AlbumId = albumId,
            TrackNumber = 2,
            IndexedFiles = [CreateIndexedFile()]
        };

        var medias = new List<BaseMedia> { artist, album, track1, track2 }.BuildMockDbSet();
        _context.Medias.Returns(medias);

        var states = new List<UserMediaState>().BuildMockDbSet();
        _context.UserMediaStates.Returns(states);

        var ratings = new List<BaseRating>().BuildMockDbSet();
        _context.Ratings.Returns(ratings);

        _currentUser.Id.Returns((Guid?)null);

        // Act
        var result = await _handler.Handle(
            new GetArtistTopTracksQuery { ArtistId = artistId, Count = 10 },
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Track One");
        result[1].Title.Should().Be("Track Two");
    }

    private static IndexedFile CreateIndexedFile() => new()
    {
        Id = Guid.NewGuid(),
        LibraryId = Guid.NewGuid(),
        Name = "track",
        Extension = ".mp3",
        Path = "/music/track.mp3",
        Hash = 1,
        Size = 1000
    };
}

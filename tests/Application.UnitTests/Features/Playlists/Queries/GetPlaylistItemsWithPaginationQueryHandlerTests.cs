using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Playlists.Queries.GetPlaylistItems;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Playlists.Queries;

[TestFixture]
public class GetPlaylistItemsWithPaginationQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private GetPlaylistItemsWithPaginationQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _playlistId;
    private Guid _movieId;

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
        _playlistId = Guid.NewGuid();
        _movieId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, DisplayName = "owner" });
        _context.Medias.Add(new Movie { Id = _movieId, Title = "Film" });
        _context.Playlists.Add(new Playlist
        {
            Id = _playlistId,
            Title = "Films",
            MediaType = MediaType.Movie,
            UserId = _userId
        });
        _context.PlaylistItems.Add(new PlaylistItem
        {
            Id = Guid.NewGuid(),
            PlaylistId = _playlistId,
            MediaId = _movieId,
            Order = 0
        });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _movieId,
            IsCompleted = true,
            PlayCount = 1
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _handler = new GetPlaylistItemsWithPaginationQueryHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldExposeWatchedStateOnItem()
    {
        var result = await _handler.Handle(new GetPlaylistItemsWithPaginationQuery
        {
            PlaylistId = _playlistId,
            PageNumber = 1,
            PageSize = 50,
            IncludeUnavailable = true
        }, CancellationToken.None);

        var dto = result.Items.Should().ContainSingle().Which.ToPlaylistItemDto();
        dto.UserState.Should().NotBeNull();
        dto.UserState!.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldExposeTheTrackDuration_WhenTheFileHasAudioMetadata()
    {
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
            Title = "Lib",
            MediaType = LibraryMediaType.Music,
            RootPath = "/media",
            MetadataProviderName = "musicbrainz",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

        var albumId = Guid.NewGuid();
        _context.Medias.Add(new MusicAlbum { Id = albumId, Title = "Album" });
        var track = new MusicTrack { Id = Guid.NewGuid(), Title = "Track", AlbumId = albumId };
        track.IndexedFiles.Add(new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            MediaId = track.Id,
            Name = "Track",
            Extension = ".mp3",
            Path = "/media/Track.mp3",
            Hash = 1,
            Size = 1,
            FileMetadata = new AudioFileMetadata
            {
                Container = "mp3",
                Duration = TimeSpan.FromSeconds(200)
            }
        });
        _context.Medias.Add(track);

        var playlistId = Guid.NewGuid();
        _context.Playlists.Add(new Playlist
        {
            Id = playlistId,
            Title = "Tracks",
            MediaType = MediaType.MusicTrack,
            UserId = _userId
        });
        _context.PlaylistItems.Add(new PlaylistItem
        {
            Id = Guid.NewGuid(),
            PlaylistId = playlistId,
            MediaId = track.Id,
            Order = 0
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _handler.Handle(new GetPlaylistItemsWithPaginationQuery
        {
            PlaylistId = playlistId,
            PageNumber = 1,
            PageSize = 50
        }, CancellationToken.None);

        var dto = result.Items.Should().ContainSingle().Which.ToPlaylistItemDto();
        dto.Duration.Should().Be(200);
    }
}

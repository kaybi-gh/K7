using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Playlists.Queries.GetPlaylists;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.SharedProfiles;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Playlists.Queries;

[TestFixture]
public class GetPlaylistsWithPaginationQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private GetPlaylistsWithPaginationQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _ownedPlaylistId;
    private Guid _otherUserId;
    private Guid _otherUserPlaylistId;
    private Guid _sharedProfileId;

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
        _otherUserId = Guid.NewGuid();
        _ownedPlaylistId = Guid.NewGuid();
        _otherUserPlaylistId = Guid.NewGuid();
        _sharedProfileId = Guid.NewGuid();

        _context.Users.AddRange(
            new User { Id = _userId, DisplayName = "member" },
            new User { Id = _otherUserId, DisplayName = "host" });

        _context.Playlists.AddRange(
            new Playlist
            {
                Id = _ownedPlaylistId,
                Title = "Mine",
                MediaType = MediaType.Movie,
                UserId = _userId,
                VisibilityScope = VisibilityScope.Nobody
            },
            new Playlist
            {
                Id = _otherUserPlaylistId,
                Title = "Shared By Host",
                MediaType = MediaType.Movie,
                UserId = _otherUserId,
                VisibilityScope = VisibilityScope.Nobody
            });

        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = _sharedProfileId,
            Name = "Family",
            HostUserId = _otherUserId,
            CreatedByUserId = _otherUserId,
            Members = new List<SharedProfileMember>
            {
                new() { UserId = _userId }
            }
        });

        _context.SharedProfilePlaylists.Add(new SharedProfilePlaylist
        {
            SharedProfileId = _sharedProfileId,
            PlaylistId = _otherUserPlaylistId
        });

        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _handler = new GetPlaylistsWithPaginationQueryHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnOnlyOwnedPlaylists_WhenNoSharedProfileActive()
    {
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var result = await _handler.Handle(
            new GetPlaylistsWithPaginationQuery { PageNumber = 1, PageSize = 10 },
            CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items.Should().ContainSingle(p => p.Id == _ownedPlaylistId);
        result.Items.Should().NotContain(p => p.Id == _otherUserPlaylistId);
    }

    [Test]
    public async Task Handle_ShouldIncludeSharedProfilePlaylists_WhenSharedProfileActive()
    {
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(_sharedProfileId);

        var result = await _handler.Handle(
            new GetPlaylistsWithPaginationQuery { PageNumber = 1, PageSize = 10 },
            CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(p => p.Id == _ownedPlaylistId);
        result.Items.Should().Contain(p => p.Id == _otherUserPlaylistId);
    }

    [Test]
    public async Task Handle_ShouldReturnItemCountAndLimitedPreviews_WhenPlaylistHasManyItems()
    {
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var pictureIds = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            var mediaId = Guid.NewGuid();
            var pictureId = Guid.NewGuid();
            pictureIds.Add(pictureId);

            _context.Medias.Add(new Movie
            {
                Id = mediaId,
                Title = $"Movie {i}"
            });
            _context.MetadataPictures.Add(new MetadataPicture
            {
                Id = pictureId,
                Type = MetadataPictureType.Poster,
                LocalPath = $"/covers/{pictureId}.jpg",
                MediaId = mediaId
            });
            _context.PlaylistItems.Add(new PlaylistItem
            {
                Id = Guid.NewGuid(),
                PlaylistId = _ownedPlaylistId,
                MediaId = mediaId,
                Order = i
            });
        }

        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetPlaylistsWithPaginationQuery { PageNumber = 1, PageSize = 10 },
            CancellationToken.None);

        var playlist = result.Items.Should().ContainSingle(p => p.Id == _ownedPlaylistId).Subject;
        playlist.ItemCount.Should().Be(12);
        playlist.PreviewPictures.Should().HaveCount(4);
        playlist.PreviewPictures.Select(p => p.Id).Should().Equal(pictureIds.Take(4));
    }

    [Test]
    public async Task Handle_ShouldSkipPreviewLookup_WhenPlaylistHasCustomCover()
    {
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var coverId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var itemPictureId = Guid.NewGuid();

        var cover = new MetadataPicture
        {
            Id = coverId,
            Type = MetadataPictureType.Cover,
            LocalPath = $"/covers/{coverId}.jpg",
            PlaylistId = _ownedPlaylistId
        };
        _context.MetadataPictures.Add(cover);

        var playlist = await _context.Playlists.SingleAsync(p => p.Id == _ownedPlaylistId);
        playlist.CoverPicture = cover;

        _context.Medias.Add(new Movie { Id = mediaId, Title = "Covered" });
        _context.MetadataPictures.Add(new MetadataPicture
        {
            Id = itemPictureId,
            Type = MetadataPictureType.Poster,
            LocalPath = $"/covers/{itemPictureId}.jpg",
            MediaId = mediaId
        });
        _context.PlaylistItems.Add(new PlaylistItem
        {
            Id = Guid.NewGuid(),
            PlaylistId = _ownedPlaylistId,
            MediaId = mediaId,
            Order = 0
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetPlaylistsWithPaginationQuery { PageNumber = 1, PageSize = 10 },
            CancellationToken.None);

        var dto = result.Items.Should().ContainSingle(p => p.Id == _ownedPlaylistId).Subject;
        dto.CoverPicture.Should().NotBeNull();
        dto.CoverPicture!.Id.Should().Be(coverId);
        dto.ItemCount.Should().Be(1);
        dto.PreviewPictures.Should().BeEmpty();
    }
}

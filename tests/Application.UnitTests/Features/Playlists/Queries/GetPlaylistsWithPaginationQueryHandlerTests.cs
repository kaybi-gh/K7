using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Playlists.Queries.GetPlaylists;
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
}

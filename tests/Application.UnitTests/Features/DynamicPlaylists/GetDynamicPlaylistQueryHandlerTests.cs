using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.DynamicPlaylists.Queries.GetDynamicPlaylist;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.DynamicPlaylists;

[TestFixture]
public class GetDynamicPlaylistQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private GetDynamicPlaylistQueryHandler _handler = null!;
    private Guid _userId;

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
        _context.Users.Add(new User { Id = _userId, DisplayName = "owner" });
        _context.SaveChanges();

        var currentUser = Substitute.For<IUser>();
        currentUser.Id.Returns(_userId);
        _handler = new GetDynamicPlaylistQueryHandler(_context, currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnTheItemCount_WhenThePlaylistHasItems()
    {
        var playlistId = SeedPlaylist(itemCount: 3);
        SeedPlaylist(itemCount: 2);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetDynamicPlaylistQuery(playlistId), CancellationToken.None);

        result.Playlist.Id.Should().Be(playlistId);
        result.ItemCount.Should().Be(3);
    }

    private Guid SeedPlaylist(int itemCount)
    {
        var playlist = new DynamicPlaylist
        {
            Id = Guid.NewGuid(),
            Title = "Favorites",
            MediaType = MediaType.Movie,
            UserId = _userId,
            RuleFilter = new RuleGroup { MatchCondition = RuleMatchCondition.All }
        };

        for (var order = 0; order < itemCount; order++)
        {
            var movie = new Movie { Id = Guid.NewGuid(), Title = $"Movie {order}" };
            _context.Medias.Add(movie);
            playlist.Items.Add(new PlaylistItem { Id = Guid.NewGuid(), MediaId = movie.Id, Order = order });
        }

        _context.Playlists.Add(playlist);
        return playlist.Id;
    }
}

using Ardalis.GuardClauses;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Playlists.Commands.UpdatePlaylist;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Playlists.Commands;

[TestFixture]
public class UpdatePlaylistCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private UpdatePlaylistCommandHandler _handler = null!;
    private Guid _userId;
    private Guid _playlistId;

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
        _context.Users.Add(new User { Id = _userId, DisplayName = "owner" });
        _context.Playlists.Add(new Playlist
        {
            Id = _playlistId,
            Title = "Movies",
            Description = "Old",
            MediaType = MediaType.Movie,
            UserId = _userId,
            VisibilityScope = VisibilityScope.Nobody
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _handler = new UpdatePlaylistCommandHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldUpdateMutableFields()
    {
        await _handler.Handle(new UpdatePlaylistCommand
        {
            Id = _playlistId,
            Title = "Renamed",
            Description = "New desc",
            MediaType = MediaType.Movie,
            VisibilityScope = VisibilityScope.LocalServer
        }, CancellationToken.None);

        var playlist = await _context.Playlists.SingleAsync(p => p.Id == _playlistId);
        playlist.Title.Should().Be("Renamed");
        playlist.Description.Should().Be("New desc");
        playlist.VisibilityScope.Should().Be(VisibilityScope.LocalServer);
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenMediaTypeChanges()
    {
        var act = () => _handler.Handle(new UpdatePlaylistCommand
        {
            Id = _playlistId,
            Title = "Movies",
            MediaType = MediaType.MusicTrack,
            VisibilityScope = VisibilityScope.Nobody
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenNotOwner()
    {
        _currentUser.Id.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new UpdatePlaylistCommand
        {
            Id = _playlistId,
            Title = "Movies",
            MediaType = MediaType.Movie
        }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

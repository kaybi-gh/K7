using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Playlists.Commands.AddPlaylistItem;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Playlists.Commands;

[TestFixture]
public class AddPlaylistItemCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private AddPlaylistItemCommandHandler _handler = null!;
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
        _context.Playlists.Add(new Playlist
        {
            Id = _playlistId,
            Title = "Movies",
            MediaType = MediaType.Movie,
            UserId = _userId
        });
        _context.Medias.Add(new Movie { Id = _movieId, Title = "Film" });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _handler = new AddPlaylistItemCommandHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldAddItemWithIncrementedOrder()
    {
        var firstId = await _handler.Handle(new AddPlaylistItemCommand
        {
            PlaylistId = _playlistId,
            MediaId = _movieId
        }, CancellationToken.None);

        var secondMovieId = Guid.NewGuid();
        _context.Medias.Add(new Movie { Id = secondMovieId, Title = "Film 2" });
        await _context.SaveChangesAsync();

        var secondId = await _handler.Handle(new AddPlaylistItemCommand
        {
            PlaylistId = _playlistId,
            MediaId = secondMovieId
        }, CancellationToken.None);

        var items = await _context.PlaylistItems.Where(i => i.PlaylistId == _playlistId).OrderBy(i => i.Order).ToListAsync();
        items.Should().HaveCount(2);
        items[0].Id.Should().Be(firstId);
        items[0].Order.Should().Be(0);
        items[1].Id.Should().Be(secondId);
        items[1].Order.Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenMediaTypeDoesNotMatch()
    {
        var albumId = Guid.NewGuid();
        _context.Medias.Add(new MusicAlbum { Id = albumId, Title = "Album" });
        await _context.SaveChangesAsync();

        var act = () => _handler.Handle(new AddPlaylistItemCommand
        {
            PlaylistId = _playlistId,
            MediaId = albumId
        }, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenPlaylistOwnedBySomeoneElse()
    {
        _currentUser.Id.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new AddPlaylistItemCommand
        {
            PlaylistId = _playlistId,
            MediaId = _movieId
        }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

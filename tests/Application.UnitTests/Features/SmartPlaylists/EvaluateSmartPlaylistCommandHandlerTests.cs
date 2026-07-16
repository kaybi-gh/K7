using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.SmartPlaylists.Commands.EvaluateSmartPlaylist;
using K7.Server.Application.Features.SmartPlaylists.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.SmartPlaylists;

[TestFixture]
public class EvaluateSmartPlaylistCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private EvaluateSmartPlaylistCommandHandler _handler = null!;
    private Guid _userId;
    private Guid _libraryId;

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
        var groupId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, DisplayName = "owner" });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            Title = "Lib",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _handler = new EvaluateSmartPlaylistCommandHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldRebuildItemsRespectingTypeLimitAndOrder()
    {
        var older = SeedMovie("Alpha", DateTime.UtcNow.AddDays(-2));
        var newer = SeedMovie("Beta", DateTime.UtcNow.AddDays(-1));
        SeedMovieWithoutFile("Gamma");
        SeedMusicTrack("Track");

        var playlistId = Guid.NewGuid();
        _context.Playlists.Add(new SmartPlaylist
        {
            Id = playlistId,
            Title = "Recent movies",
            MediaType = MediaType.Movie,
            UserId = _userId,
            Limit = 1,
            OrderBy = SmartPlaylistOrderBy.DateAdded,
            OrderDescending = true,
            RuleFilter = new RuleGroup { MatchCondition = RuleMatchCondition.All }
        });
        await _context.SaveChangesAsync();

        var resultId = await _handler.Handle(new EvaluateSmartPlaylistCommand { Id = playlistId }, CancellationToken.None);

        resultId.Should().Be(playlistId);
        var playlist = await _context.Playlists.OfType<SmartPlaylist>()
            .Include(p => p.Items)
            .SingleAsync(p => p.Id == playlistId);
        playlist.Items.Should().ContainSingle();
        playlist.Items.Single().MediaId.Should().Be(newer.Id);
        playlist.LastEvaluatedAt.Should().NotBeNull();
        playlist.Items.Should().NotContain(i => i.MediaId == older.Id);
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenNotOwner()
    {
        var playlistId = Guid.NewGuid();
        _context.Playlists.Add(new SmartPlaylist
        {
            Id = playlistId,
            Title = "Mine",
            MediaType = MediaType.Movie,
            UserId = _userId
        });
        await _context.SaveChangesAsync();

        _currentUser.Id.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new EvaluateSmartPlaylistCommand { Id = playlistId }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void ApplyRules_ShouldOrderByTitleAscending()
    {
        var medias = new List<BaseMedia>
        {
            new Movie { Id = Guid.NewGuid(), Title = "Charlie", SortTitle = "Charlie" },
            new Movie { Id = Guid.NewGuid(), Title = "Alpha", SortTitle = "Alpha" },
            new MusicTrack { Id = Guid.NewGuid(), Title = "Song", AlbumId = Guid.NewGuid() }
        }.AsQueryable();

        var playlist = new SmartPlaylist
        {
            Title = "Titles",
            MediaType = MediaType.Movie,
            UserId = _userId,
            OrderBy = SmartPlaylistOrderBy.Title,
            OrderDescending = false,
            RuleFilter = new RuleGroup { MatchCondition = RuleMatchCondition.All }
        };

        var result = SmartPlaylistEvaluator.ApplyRules(medias, playlist, _userId).ToList();

        result.Should().HaveCount(2);
        result.Select(m => m.Title).Should().Equal("Alpha", "Charlie");
    }

    private Movie SeedMovie(string title, DateTime created)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = title,
            Created = created
        };
        movie.IndexedFiles.Add(new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            MediaId = movie.Id,
            Name = title,
            Extension = ".mkv",
            Path = $"/media/{title}.mkv",
            Hash = 1,
            Size = 1
        });
        _context.Medias.Add(movie);
        return movie;
    }

    private void SeedMovieWithoutFile(string title)
    {
        _context.Medias.Add(new Movie { Id = Guid.NewGuid(), Title = title });
    }

    private void SeedMusicTrack(string title)
    {
        var albumId = Guid.NewGuid();
        _context.Medias.Add(new MusicAlbum { Id = albumId, Title = "Album" });
        var track = new MusicTrack
        {
            Id = Guid.NewGuid(),
            Title = title,
            AlbumId = albumId
        };
        track.IndexedFiles.Add(new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            MediaId = track.Id,
            Name = title,
            Extension = ".mp3",
            Path = $"/media/{title}.mp3",
            Hash = 1,
            Size = 1
        });
        _context.Medias.Add(track);
    }
}

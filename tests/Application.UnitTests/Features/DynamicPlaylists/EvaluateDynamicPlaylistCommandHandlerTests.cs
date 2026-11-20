using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.DynamicPlaylists.Commands.EvaluateDynamicPlaylist;
using K7.Server.Application.Features.DynamicPlaylists.Services;
using K7.Server.Domain.Entities;
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
public class EvaluateDynamicPlaylistCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private EvaluateDynamicPlaylistCommandHandler _handler = null!;
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
        _handler = new EvaluateDynamicPlaylistCommandHandler(_context, _currentUser, new MediaAccessFilter(_context));
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
        _context.Playlists.Add(new DynamicPlaylist
        {
            Id = playlistId,
            Title = "Recent movies",
            MediaType = MediaType.Movie,
            UserId = _userId,
            Limit = 1,
            OrderBy = DynamicPlaylistOrderBy.DateAdded,
            OrderDescending = true,
            RuleFilter = new RuleGroup { MatchCondition = RuleMatchCondition.All }
        });
        await _context.SaveChangesAsync();

        var resultId = await _handler.Handle(new EvaluateDynamicPlaylistCommand { Id = playlistId }, CancellationToken.None);

        resultId.Should().Be(playlistId);
        var playlist = await _context.Playlists.OfType<DynamicPlaylist>()
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
        _context.Playlists.Add(new DynamicPlaylist
        {
            Id = playlistId,
            Title = "Mine",
            MediaType = MediaType.Movie,
            UserId = _userId
        });
        await _context.SaveChangesAsync();

        _currentUser.Id.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new EvaluateDynamicPlaylistCommand { Id = playlistId }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ShouldOmitTracks_WhenLibraryIsExcluded()
    {
        var albumId = Guid.NewGuid();
        _context.Medias.Add(new MusicAlbum { Id = albumId, Title = "Album" });

        var publicLibraryId = await SeedMusicLibraryAsync("Public");
        var privateLibraryId = await SeedMusicLibraryAsync("Private");
        var visibleId = SeedLinkedTrack("PUBLIC-TRACK", publicLibraryId, albumId);
        var hiddenId = SeedLinkedTrack("PRIVATE-MARKER-TRACK", privateLibraryId, albumId);
        _context.UserLibraryExclusions.Add(new UserLibraryExclusion
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            LibraryId = privateLibraryId,
            IsAdminExcluded = true
        });

        var playlistId = Guid.NewGuid();
        _context.Playlists.Add(new DynamicPlaylist
        {
            Id = playlistId,
            Title = "All music",
            MediaType = MediaType.MusicTrack,
            UserId = _userId,
            RuleFilter = new RuleGroup { MatchCondition = RuleMatchCondition.All }
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(new EvaluateDynamicPlaylistCommand { Id = playlistId }, CancellationToken.None);

        var playlist = await _context.Playlists.OfType<DynamicPlaylist>()
            .Include(p => p.Items)
            .SingleAsync(p => p.Id == playlistId);
        playlist.Items.Should().ContainSingle();
        playlist.Items.Single().MediaId.Should().Be(visibleId);
        playlist.Items.Should().NotContain(i => i.MediaId == hiddenId);
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

        var playlist = new DynamicPlaylist
        {
            Title = "Titles",
            MediaType = MediaType.Movie,
            UserId = _userId,
            OrderBy = DynamicPlaylistOrderBy.Title,
            OrderDescending = false,
            RuleFilter = new RuleGroup { MatchCondition = RuleMatchCondition.All }
        };

        var result = DynamicPlaylistEvaluator.ApplyRules(medias, playlist, _userId).ToList();

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
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            MediaId = movie.Id,
            LibraryId = _libraryId
        });
        return movie;
    }

    private async Task<Guid> SeedMusicLibraryAsync(string title)
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = title,
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = title,
            MediaType = LibraryMediaType.Music,
            RootPath = $"/{title}",
            MetadataProviderName = "none",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        await _context.SaveChangesAsync();
        return libraryId;
    }

    private Guid SeedLinkedTrack(string title, Guid libraryId, Guid albumId)
    {
        var track = new MusicTrack
        {
            Id = Guid.NewGuid(),
            Title = title,
            AlbumId = albumId
        };
        track.IndexedFiles.Add(new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            MediaId = track.Id,
            Name = title,
            Extension = ".mp3",
            Path = $"/media/{title}.mp3",
            Hash = 1,
            Size = 1
        });
        _context.Medias.Add(track);
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            MediaId = track.Id,
            LibraryId = libraryId
        });
        return track.Id;
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

using AwesomeAssertions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Devices.Commands.EnsureOpenSubsonicDevice;
using K7.Server.Application.Features.OpenSubsonic;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.OpenSubsonic;

[TestFixture]
public class OpenSubsonicPlayQueueTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private IUser _currentUser = null!;
    private OpenSubsonicService _service = null!;
    private Guid _userId;
    private Guid _albumId;
    private Guid _libraryId;
    private Guid _deviceA;
    private Guid _deviceB;

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
        _albumId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
        _deviceA = Guid.NewGuid();
        _deviceB = Guid.NewGuid();

        var user = new User { Id = _userId, IdentityUserId = "ident", DisplayName = "listener" };
        var groupId = Guid.NewGuid();
        _context.Users.Add(user);
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
            Title = "Music",
            MediaType = LibraryMediaType.Music,
            MetadataProviderName = "none",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.Medias.Add(new MusicAlbum { Id = _albumId, Title = "Album" });
        _context.Devices.Add(ExternalDevice(_deviceA, "Tempus", user));
        _context.Devices.Add(ExternalDevice(_deviceB, "Feishin", user));
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetIdAsync(Arg.Any<CancellationToken>()).Returns(_userId);

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<EnsureOpenSubsonicDeviceCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<EnsureOpenSubsonicDeviceCommand>().ClientName == "Feishin" ? _deviceB : _deviceA);

        _service = new OpenSubsonicService(
            _context,
            _currentUser,
            Substitute.For<IMediaAccessGuard>(),
            new MediaAccessFilter(_context),
            _sender,
            Substitute.For<IActiveStreamTracker>(),
            Substitute.For<IOpenSubsonicAudioTranscoder>(),
            NullLogger<OpenSubsonicService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task SaveAndGet_ShouldRoundTripTheQueueForThatClient()
    {
        var first = await AddTrackAsync("One");
        var second = await AddTrackAsync("Two");

        var save = await _service.ExecuteAsync(
            "savePlayQueue",
            new Dictionary<string, string[]>
            {
                ["id"] = [first.ToString("D"), second.ToString("D")],
                ["current"] = [second.ToString("D")],
                ["position"] = ["15000"],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        save.IsFailed.Should().BeFalse();

        var get = await _service.ExecuteAsync(
            "getPlayQueue",
            new Dictionary<string, string[]> { ["c"] = ["Tempus"] },
            "listener",
            canWrite: true);

        get.IsFailed.Should().BeFalse();
        var queue = QueueOf(get);
        queue["current"].Should().Be(second.ToString("D"));
        queue["position"].Should().Be(15000);
        queue["changedBy"].Should().Be("Tempus");
        SongsOf(queue).Select(s => s.Id).Should().Equal(first.ToString("D"), second.ToString("D"));
    }

    [Test]
    public async Task SaveAndGetByIndex_ShouldKeepTheSecondCopyOfTheSameSong()
    {
        var song = await AddTrackAsync("Repeat");

        var save = await _service.ExecuteAsync(
            "savePlayQueueByIndex",
            new Dictionary<string, string[]>
            {
                ["id"] = [song.ToString("D"), song.ToString("D")],
                ["currentIndex"] = ["1"],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        save.IsFailed.Should().BeFalse();

        var get = await _service.ExecuteAsync(
            "getPlayQueueByIndex",
            new Dictionary<string, string[]> { ["c"] = ["Tempus"] },
            "listener",
            canWrite: true);

        var queue = QueueOf(get);
        queue["currentIndex"].Should().Be(1);
        SongsOf(queue).Should().HaveCount(2);
        SongsOf(queue).Should().OnlyContain(s => s.Id == song.ToString("D"));
    }

    [Test]
    public async Task Save_ShouldIsolateQueues_WhenClientNamesDiffer()
    {
        var alpha = await AddTrackAsync("Alpha");
        var beta = await AddTrackAsync("Beta");

        await _service.ExecuteAsync(
            "savePlayQueue",
            new Dictionary<string, string[]>
            {
                ["id"] = [alpha.ToString("D")],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);
        await _service.ExecuteAsync(
            "savePlayQueue",
            new Dictionary<string, string[]>
            {
                ["id"] = [beta.ToString("D")],
                ["c"] = ["Feishin"]
            },
            "listener",
            canWrite: true);

        SongsOf(QueueOf(await GetAsync("Tempus"))).Select(s => s.Id).Should().Equal(alpha.ToString("D"));
        SongsOf(QueueOf(await GetAsync("Feishin"))).Select(s => s.Id).Should().Equal(beta.ToString("D"));
    }

    [Test]
    public async Task Save_ShouldCapAnAdHocQueueAtOneHundred()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 120; i++)
            ids.Add(await AddTrackAsync($"Song {i}"));

        var current = ids[110];
        var save = await _service.ExecuteAsync(
            "savePlayQueue",
            new Dictionary<string, string[]>
            {
                ["id"] = ids.Select(id => id.ToString("D")).ToArray(),
                ["current"] = [current.ToString("D")],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        save.IsFailed.Should().BeFalse();
        var songs = SongsOf(QueueOf(await GetAsync("Tempus")));
        songs.Should().HaveCount(100);
        songs[0].Id.Should().Be(ids[20].ToString("D"));
        songs.Should().Contain(s => s.Id == current.ToString("D"));
    }

    [Test]
    public async Task SaveAndGet_ShouldOmitTracks_WhenLibraryIsExcluded()
    {
        var visible = await AddTrackAsync("PUBLIC-TRACK");
        var hiddenLibraryId = await AddLibraryAsync("Private");
        var hidden = await AddTrackAsync("PRIVATE-MARKER-TRACK", hiddenLibraryId);
        _context.UserLibraryExclusions.Add(new UserLibraryExclusion
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            LibraryId = hiddenLibraryId,
            IsAdminExcluded = true
        });
        await _context.SaveChangesAsync();

        var save = await _service.ExecuteAsync(
            "savePlayQueue",
            new Dictionary<string, string[]>
            {
                ["id"] = [visible.ToString("D"), hidden.ToString("D")],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        save.IsFailed.Should().BeFalse();
        var songs = SongsOf(QueueOf(await GetAsync("Tempus")));
        songs.Select(s => s.Title).Should().Equal("PUBLIC-TRACK");
    }

    [Test]
    public async Task GetPlaylist_ShouldOmitTracks_WhenLibraryIsExcluded()
    {
        var visible = await AddTrackAsync("PUBLIC-TRACK");
        var hiddenLibraryId = await AddLibraryAsync("Private");
        var hidden = await AddTrackAsync("PRIVATE-MARKER-TRACK", hiddenLibraryId);
        _context.UserLibraryExclusions.Add(new UserLibraryExclusion
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            LibraryId = hiddenLibraryId,
            IsAdminExcluded = true
        });

        var playlistId = Guid.NewGuid();
        _context.Playlists.Add(new Playlist
        {
            Id = playlistId,
            Title = "mix",
            MediaType = MediaType.MusicTrack,
            UserId = _userId,
            Items =
            [
                new PlaylistItem { Id = Guid.NewGuid(), MediaId = visible, Order = 0 },
                new PlaylistItem { Id = Guid.NewGuid(), MediaId = hidden, Order = 1 }
            ]
        });
        await _context.SaveChangesAsync();

        var detail = await _service.ExecuteAsync(
            "getPlaylist",
            new Dictionary<string, string[]> { ["id"] = [playlistId.ToString("D")] },
            "listener",
            canWrite: true);

        detail.IsFailed.Should().BeFalse();
        var playlist = (OpenSubsonicPlaylist)detail.Data!["playlist"]!;
        playlist.SongCount.Should().Be(1);
        playlist.Entry.Should().ContainSingle();
        playlist.Entry![0].Title.Should().Be("PUBLIC-TRACK");
        playlist.Entry.Should().NotContain(s => s.Title == "PRIVATE-MARKER-TRACK");

        var list = await _service.ExecuteAsync(
            "getPlaylists",
            new Dictionary<string, string[]>(),
            "listener",
            canWrite: true);
        var wrapped = (Dictionary<string, object?>)list.Data!["playlists"]!;
        var cards = (List<OpenSubsonicPlaylist>)wrapped["playlist"]!;
        cards.Should().ContainSingle();
        cards[0].SongCount.Should().Be(1);
    }

    private async Task<OpenSubsonicActionResult> GetAsync(string client) =>
        await _service.ExecuteAsync(
            "getPlayQueue",
            new Dictionary<string, string[]> { ["c"] = [client] },
            "listener",
            canWrite: true);

    private Task<Guid> AddTrackAsync(string title) => AddTrackAsync(title, _libraryId);

    private async Task<Guid> AddTrackAsync(string title, Guid libraryId)
    {
        var id = Guid.NewGuid();
        _context.Medias.Add(new MusicTrack
        {
            Id = id,
            Title = title,
            AlbumId = _albumId
        });
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            MediaId = id,
            LibraryId = libraryId
        });
        await _context.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> AddLibraryAsync(string title)
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
            MetadataProviderName = "none",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        await _context.SaveChangesAsync();
        return libraryId;
    }

    private static Device ExternalDevice(Guid id, string name, User user) =>
        new()
        {
            Id = id,
            ClientType = ClientType.External,
            DeviceName = name,
            Users = [user]
        };

    private static Dictionary<string, object?> QueueOf(OpenSubsonicActionResult result)
    {
        result.IsFailed.Should().BeFalse();
        return (Dictionary<string, object?>)result.Data!["playQueue"]!;
    }

    private static List<OpenSubsonicSong> SongsOf(Dictionary<string, object?> queue) =>
        (List<OpenSubsonicSong>)queue["entry"]!;
}

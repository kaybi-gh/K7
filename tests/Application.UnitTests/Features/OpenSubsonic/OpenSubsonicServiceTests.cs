using FluentAssertions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.OpenSubsonic;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.OpenSubsonic;

[TestFixture]
public class OpenSubsonicServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private OpenSubsonicService _service = null!;

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

        var currentUser = Substitute.For<IUser>();
        currentUser.Id.Returns((Guid?)null);
        currentUser.IdentityId.Returns((string?)null);

        var accessGuard = Substitute.For<IMediaAccessGuard>();
        var mediaAccessFilter = new MediaAccessFilter(_context);
        var sender = Substitute.For<ISender>();
        var activeStreams = Substitute.For<IActiveStreamTracker>();
        var transcoder = Substitute.For<K7.Server.Domain.Interfaces.IOpenSubsonicAudioTranscoder>();

        _service = new OpenSubsonicService(
            _context,
            currentUser,
            accessGuard,
            mediaAccessFilter,
            sender,
            activeStreams,
            transcoder,
            NullLogger<OpenSubsonicService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_ShouldReturnOk_ForPing()
    {
        var result = await _service.ExecuteAsync(
            "ping.view",
            new Dictionary<string, string[]>(),
            "tester",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        result.Data.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_ShouldAdvertiseApiKeyExtension()
    {
        var result = await _service.ExecuteAsync(
            "getOpenSubsonicExtensions",
            new Dictionary<string, string[]>(),
            "tester",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        result.Data.Should().ContainKey("openSubsonicExtensions");
    }

    [Test]
    public async Task ExecuteAsync_ShouldReturnNotFound_ForUnsupportedVideoAction()
    {
        var result = await _service.ExecuteAsync(
            "getVideos",
            new Dictionary<string, string[]>(),
            "tester",
            canWrite: true);

        result.IsFailed.Should().BeTrue();
        result.Error!.Code.Should().Be(OpenSubsonicConstants.ErrorNotFound);
    }

    [Test]
    public async Task ExecuteAsync_ShouldForbidWrite_WhenCanWriteFalse()
    {
        var result = await _service.ExecuteAsync(
            "createPlaylist",
            new Dictionary<string, string[]> { ["name"] = ["Test"] },
            "tester",
            canWrite: false);

        result.IsFailed.Should().BeTrue();
        result.Error!.Code.Should().Be(OpenSubsonicConstants.ErrorUnauthorized);
    }

    [Test]
    public async Task ExecuteAsync_ShouldReturnStableGuidIds_ForMusicFolders()
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();

        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Music group",
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            Title = "Music",
            RootPath = "/music",
            MediaType = LibraryMediaType.Music,
            MetadataProviderName = "MusicBrainz",
            MetadataLanguage = "en",
            MetadataFallbackLanguage = "en",
            LibraryGroupId = groupId
        });
        await _context.SaveChangesAsync();

        var result = await _service.ExecuteAsync(
            "getMusicFolders",
            new Dictionary<string, string[]>(),
            "tester",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        result.Data.Should().ContainKey("musicFolders");
        var wrapper = result.Data!["musicFolders"] as Dictionary<string, object?>;
        wrapper.Should().NotBeNull();
        var folders = wrapper!["musicFolder"] as List<OpenSubsonicMusicFolder>;
        folders.Should().NotBeNull();
        folders!.Should().ContainSingle(f => f.Id == libraryId.ToString("D") && f.Name == "Music");
    }

    [Test]
    public async Task ExecuteAsync_ShouldReturnNotFound_ForPlayQueue()
    {
        var get = await _service.ExecuteAsync(
            "getPlayQueue",
            new Dictionary<string, string[]>(),
            "tester",
            canWrite: true);
        var save = await _service.ExecuteAsync(
            "savePlayQueue",
            new Dictionary<string, string[]>(),
            "tester",
            canWrite: true);

        get.IsFailed.Should().BeTrue();
        get.Error!.Code.Should().Be(OpenSubsonicConstants.ErrorNotFound);
        save.IsFailed.Should().BeTrue();
        save.Error!.Code.Should().Be(OpenSubsonicConstants.ErrorNotFound);
    }

    [Test]
    public async Task TokenInfo_ShouldEchoUsername()
    {
        var result = await _service.ExecuteAsync(
            "tokenInfo",
            new Dictionary<string, string[]>(),
            "api-user",
            canWrite: false);

        result.IsFailed.Should().BeFalse();
        result.Data.Should().ContainKey("tokenInfo");
    }
}

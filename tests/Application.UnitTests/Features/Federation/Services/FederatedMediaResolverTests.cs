using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Federation.Services;

[TestFixture]
public class FederatedMediaResolverTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private FederatedMediaResolver _resolver = null!;

    private Guid _peerServerId;
    private Guid _remoteMediaId;
    private Guid _localMediaId;
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

        _resolver = new FederatedMediaResolver(_context);

        _peerServerId = Guid.NewGuid();
        _remoteMediaId = Guid.NewGuid();
        _localMediaId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();

        var libraryGroupId = Guid.NewGuid();
        _context.PeerServers.Add(new PeerServer
        {
            Id = _peerServerId,
            Name = "Peer",
            BaseUrl = "https://peer.example"
        });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = libraryGroupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = libraryGroupId,
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            Title = "Movies"
        });

        var movie = new Movie { Id = _localMediaId, Title = "Inception" };
        _context.Medias.Add(movie);
        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnResolvedLocal_WhenTmdbExternalIdMatches()
    {
        _context.ExternalIds.Add(new ExternalId
        {
            ProviderName = "tmdb",
            Value = "27205",
            MediaId = _localMediaId
        });
        await _context.SaveChangesAsync();

        var mediaRef = new FederatedMediaRef
        {
            RemoteMediaId = _remoteMediaId,
            Type = MediaType.Movie,
            ExternalIds =
            [
                new PeerExternalIdDto { Provider = "tmdb", Value = "27205" }
            ]
        };

        var result = await _resolver.ResolveAsync(_peerServerId, mediaRef);

        result.Status.Should().Be(FederatedMediaResolutionStatus.ResolvedLocal);
        result.LocalMediaId.Should().Be(_localMediaId);
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnResolvedRemote_WhenFederationIdAndRemoteFileExist()
    {
        var federationMediaId = Guid.NewGuid();
        var federationMovie = new Movie { Id = federationMediaId, Title = "Federated Inception" };
        _context.Medias.Add(federationMovie);

        var remoteFileId = Guid.NewGuid();
        _context.ExternalIds.Add(new ExternalId
        {
            ProviderName = "federation",
            Value = $"{_peerServerId}:{_remoteMediaId}",
            MediaId = federationMediaId
        });
        _context.RemoteIndexedFiles.Add(new RemoteIndexedFile
        {
            PeerServerId = _peerServerId,
            RemoteFileId = Guid.NewGuid(),
            Name = "inception.mkv",
            Extension = ".mkv",
            Size = 1024,
            MediaId = federationMediaId,
            RemoteMediaId = _remoteMediaId,
            LibraryId = _libraryId,
            RemoteLibraryId = Guid.NewGuid()
        });
        await _context.SaveChangesAsync();

        var mediaRef = new FederatedMediaRef
        {
            RemoteMediaId = _remoteMediaId,
            Type = MediaType.Movie,
            ExternalIds = []
        };

        var result = await _resolver.ResolveAsync(_peerServerId, mediaRef);

        result.Status.Should().Be(FederatedMediaResolutionStatus.ResolvedRemote);
        result.LocalMediaId.Should().Be(federationMediaId);
        result.RemoteIndexedFileId.Should().NotBeNull();
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnUnavailable_WhenNoMatchExists()
    {
        var mediaRef = new FederatedMediaRef
        {
            RemoteMediaId = _remoteMediaId,
            Type = MediaType.Movie,
            ExternalIds =
            [
                new PeerExternalIdDto { Provider = "tmdb", Value = "99999" }
            ]
        };

        var result = await _resolver.ResolveAsync(_peerServerId, mediaRef);

        result.Status.Should().Be(FederatedMediaResolutionStatus.Unavailable);
        result.LocalMediaId.Should().BeNull();
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnResolvedRemote_WhenOnlyRemoteIndexedFileExists()
    {
        var remoteFileId = Guid.NewGuid();
        _context.RemoteIndexedFiles.Add(new RemoteIndexedFile
        {
            Id = remoteFileId,
            PeerServerId = _peerServerId,
            RemoteFileId = Guid.NewGuid(),
            Name = "remote.mkv",
            Extension = ".mkv",
            Size = 2048,
            MediaId = _localMediaId,
            RemoteMediaId = _remoteMediaId,
            LibraryId = _libraryId,
            RemoteLibraryId = Guid.NewGuid()
        });
        await _context.SaveChangesAsync();

        var mediaRef = new FederatedMediaRef
        {
            RemoteMediaId = _remoteMediaId,
            Type = MediaType.Movie,
            ExternalIds = []
        };

        var result = await _resolver.ResolveAsync(_peerServerId, mediaRef);

        result.Status.Should().Be(FederatedMediaResolutionStatus.ResolvedRemote);
        result.LocalMediaId.Should().Be(_localMediaId);
        result.RemoteIndexedFileId.Should().Be(remoteFileId);
    }
}

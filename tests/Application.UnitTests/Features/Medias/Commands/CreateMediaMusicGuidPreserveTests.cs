using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class CreateMediaMusicGuidPreserveTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private IServiceProvider _serviceProvider = null!;
    private ISender _sender = null!;
    private IMusicIntelligenceCatalogReconciler _reconciler = null!;
    private IMetadataProvider<ExternalMusicAlbumMetadata> _albumProvider = null!;
    private CreateMediaCommandHandler _handler = null!;
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

        var groupId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
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
            RootPath = "/music",
            MetadataProviderName = "musicbrainz",
            MetadataLanguage = "en",
            MetadataFallbackLanguage = "en",
            MusicAudioAnalysisEnabled = false
        });
        _context.SaveChanges();

        _albumProvider = Substitute.For<IMetadataProvider<ExternalMusicAlbumMetadata>>();
        _albumProvider.ProviderName.Returns("musicbrainz");
        _albumProvider.SearchAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns("mbid-correct");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("musicbrainz", _albumProvider);
        _serviceProviderRoot = services.BuildServiceProvider();
        _serviceProvider = _serviceProviderRoot;

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));
        _reconciler = Substitute.For<IMusicIntelligenceCatalogReconciler>();

        var tagReader = Substitute.For<IAudioTagReader>();
        tagReader.ReadTags(Arg.Any<string>()).Returns((AudioTagData?)null);

        var availability = new MediaLibraryAvailabilityService(
            _context,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

        var serieIdentity = new SerieMetadataIdentityService(
            Enumerable.Empty<ISearchableMetadataProvider>(),
            _serviceProvider,
            Substitute.For<ILogger<SerieMetadataIdentityService>>());

        _handler = new CreateMediaCommandHandler(
            _context,
            _sender,
            _serviceProvider,
            tagReader,
            Options.Create(new PathsConfiguration { Metadatas = Path.GetTempPath() }),
            Substitute.For<IMediaMetadataTagSyncService>(),
            new MediaIdentityLookupService(_context),
            new MediaIdentityLock(),
            availability,
            serieIdentity,
            new MusicMetadataIdentityService(_serviceProvider, Substitute.For<ILogger<MusicMetadataIdentityService>>()),
            _reconciler,
            Substitute.For<ILogger<CreateMediaCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
        _serviceProviderRoot.Dispose();
    }

    [Test]
    public async Task Handle_ShouldPreserveTrackAndAlbumGuids_WhenFormerMediaIdsProvidedAndExternalIdFree()
    {
        var albumId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var album = new MusicAlbum { Id = albumId, Title = "Album" };
        album.ExternalIds.Add(new ExternalId { ProviderName = "musicbrainz", Value = "mbid-wrong" });
        var track = new MusicTrack
        {
            Id = trackId,
            Title = "Song",
            TrackNumber = 1,
            AlbumId = albumId,
            Album = album
        };
        _context.Medias.Add(album);
        _context.Medias.Add(track);
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = fileId,
            LibraryId = _libraryId,
            Path = "/music/Album/01 - Song.flac",
            Name = "01 - Song.flac",
            Extension = ".flac",
            Hash = 1,
            Size = 1,
            MediaId = null,
            Identification = new MediaIdentification("Song")
            {
                Title = "Song",
                AlbumName = "Album",
                ArtistName = "Artist",
                TrackNumber = 1
            }
        });
        await _context.SaveChangesAsync();

        var returnedAlbumId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.MusicAlbum,
            LibraryId = _libraryId,
            IndexedFileIds = [fileId],
            FormerMediaIdsByIndexedFileId = new Dictionary<Guid, Guid> { [fileId] = trackId }
        }, CancellationToken.None);

        returnedAlbumId.Should().Be(albumId);
        var file = await _context.IndexedFiles.SingleAsync(f => f.Id == fileId);
        file.MediaId.Should().Be(trackId);

        var updatedAlbum = await _context.Medias.OfType<MusicAlbum>()
            .Include(a => a.ExternalIds)
            .SingleAsync(a => a.Id == albumId);
        updatedAlbum.ExternalIds.Should().ContainSingle(e => e.Value == "mbid-correct");

        (await _context.Medias.OfType<MusicTrack>().CountAsync()).Should().Be(1);
        _reconciler.DidNotReceive().RequestReconcile();
    }
}

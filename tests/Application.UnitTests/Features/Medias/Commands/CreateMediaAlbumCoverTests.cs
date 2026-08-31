using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Features.MetadataPictures.Commands.GenerateMetadataPictureVariants;
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
public class CreateMediaAlbumCoverTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private ISender _sender = null!;
    private IAudioTagReader _tagReader = null!;
    private CreateMediaCommandHandler _handler = null!;
    private Guid _libraryId;
    private string _tempRoot = null!;
    private string _libraryDirectory = null!;
    private string _metadatasDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "k7-cover-tests", Guid.NewGuid().ToString());
        _libraryDirectory = Path.Combine(_tempRoot, "library", "Album");
        _metadatasDirectory = Path.Combine(_tempRoot, "metadatas");
        Directory.CreateDirectory(_libraryDirectory);
        Directory.CreateDirectory(_metadatasDirectory);

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
            RootPath = _tempRoot,
            MetadataProviderName = "musicbrainz",
            MetadataLanguage = "en",
            MetadataFallbackLanguage = "en",
            MusicAudioAnalysisEnabled = false
        });
        _context.SaveChanges();

        var albumProvider = Substitute.For<IMetadataProvider<ExternalMusicAlbumMetadata>>();
        albumProvider.ProviderName.Returns("musicbrainz");
        albumProvider.SearchAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var services = new ServiceCollection();
        services.AddKeyedSingleton("musicbrainz", albumProvider);
        _serviceProviderRoot = services.BuildServiceProvider();

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        _tagReader = Substitute.For<IAudioTagReader>();
        _tagReader.ReadTags(Arg.Any<string>(), Arg.Any<bool>()).Returns(new AudioTagData
        {
            Title = "Song",
            Album = "Album",
            AlbumArtists = ["Artist"],
            Artists = ["Artist"],
            TrackNumber = 1
        });

        var availability = new MediaLibraryAvailabilityService(
            _context,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

        var serieIdentity = new SerieMetadataIdentityService(
            Enumerable.Empty<ISearchableMetadataProvider>(),
            _serviceProviderRoot,
            Substitute.For<ILogger<SerieMetadataIdentityService>>());

        _handler = new CreateMediaCommandHandler(
            _context,
            _sender,
            _serviceProviderRoot,
            _tagReader,
            Options.Create(new PathsConfiguration { Metadatas = _metadatasDirectory }),
            Substitute.For<IMediaMetadataTagSyncService>(),
            new MediaIdentityLookupService(_context),
            new MediaIdentityLock(),
            availability,
            serieIdentity,
            new MusicMetadataIdentityService(
                _serviceProviderRoot,
                Substitute.For<ILogger<MusicMetadataIdentityService>>()),
            Substitute.For<IMusicIntelligenceCatalogReconciler>(),
            new PlaybackBookmarkService(_context, Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaybackBookmarkService>.Instance),
            Substitute.For<ILogger<CreateMediaCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
        _serviceProviderRoot.Dispose();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public async Task Handle_ShouldCopyLocalCoverIntoMetadataDirectory_WhenSidecarArtExists()
    {
        var coverBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var sidecarPath = Path.Combine(_libraryDirectory, "folder.jpg");
        await File.WriteAllBytesAsync(sidecarPath, coverBytes);

        var fileId = Guid.NewGuid();
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = fileId,
            LibraryId = _libraryId,
            Path = Path.Combine(_libraryDirectory, "01 - Song.flac"),
            Name = "01 - Song.flac",
            Extension = ".flac",
            Hash = 1,
            Size = 1,
            Identification = new MediaIdentification("Song")
            {
                Title = "Song",
                AlbumName = "Album",
                ArtistName = "Artist",
                TrackNumber = 1
            }
        });
        await _context.SaveChangesAsync();

        var albumId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.MusicAlbum,
            LibraryId = _libraryId,
            IndexedFileIds = [fileId]
        }, CancellationToken.None);

        var album = await _context.Medias.OfType<MusicAlbum>()
            .Include(a => a.Pictures)
            .SingleAsync(a => a.Id == albumId);

        var picture = album.Pictures.Should().ContainSingle().Subject;
        picture.Type.Should().Be(MetadataPictureType.Cover);
        picture.LocalPath.Should().Be(
            Path.Combine(_metadatasDirectory, "medias", albumId.ToString(), "cover.jpg"));
        File.Exists(picture.LocalPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(picture.LocalPath!)).Should().Equal(coverBytes);

        // Variant generation later converts and deletes its source, so the picture
        // must not point at the library sidecar and the sidecar must be untouched.
        File.Exists(sidecarPath).Should().BeTrue();

        _sender.ReceivedCalls()
            .Select(c => c.GetArguments().ElementAtOrDefault(0))
            .OfType<CreateBackgroundTaskCommand>()
            .Select(c => c.Request)
            .OfType<GenerateMetadataPictureVariantsCommand>()
            .Should().ContainSingle(r => r.MetadataPictureId == picture.Id);
    }
}

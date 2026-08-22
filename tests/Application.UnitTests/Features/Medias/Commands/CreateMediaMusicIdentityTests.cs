using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
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
public class CreateMediaMusicIdentityTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private ISender _sender = null!;
    private IMetadataProvider<ExternalMusicAlbumMetadata> _albumProvider = null!;
    private IAudioTagReader _tagReader = null!;
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
            .Returns("rg-from-search");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("musicbrainz", _albumProvider);
        _serviceProviderRoot = services.BuildServiceProvider();

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        _tagReader = Substitute.For<IAudioTagReader>();
        _tagReader.ReadTags(Arg.Any<string>(), Arg.Any<bool>()).Returns((AudioTagData?)null);

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
            Options.Create(new PathsConfiguration { Metadatas = Path.GetTempPath() }),
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
    }

    [Test]
    public async Task Handle_ShouldUseTagReleaseGroup_WithoutProviderSearch()
    {
        var fileId = Guid.NewGuid();
        _tagReader.ReadTags(Arg.Any<string>(), Arg.Any<bool>()).Returns(new AudioTagData
        {
            Title = "Bye Bye Bye",
            Album = "No Strings Attached",
            AlbumArtists = ["*NSYNC"],
            Artists = ["*NSYNC"],
            TrackNumber = 1,
            MusicBrainzReleaseGroupId = "rg-from-tags",
            MusicBrainzAlbumArtistId = "artist-from-tags",
            MusicBrainzReleaseId = "release-from-tags"
        });

        _context.IndexedFiles.Add(CreateIndexedFile(fileId, "Bye Bye Bye", "No Strings Attached", "*NSYNC"));
        await _context.SaveChangesAsync();

        var albumId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.MusicAlbum,
            LibraryId = _libraryId,
            IndexedFileIds = [fileId]
        }, CancellationToken.None);

        await _albumProvider.DidNotReceiveWithAnyArgs()
            .SearchAsync(default!, default, default, default);

        var album = await _context.Medias.OfType<MusicAlbum>()
            .Include(a => a.ExternalIds)
            .Include(a => a.Artist!)
                .ThenInclude(a => a.ExternalIds)
            .SingleAsync(a => a.Id == albumId);

        album.ExternalIds.Should().Contain(e => e.ProviderName == "musicbrainz" && e.Value == "rg-from-tags");
        album.ExternalIds.Should().Contain(e => e.ProviderName == "musicbrainz-release" && e.Value == "release-from-tags");
        album.Artist.Should().NotBeNull();
        album.Artist!.ExternalIds.Should().Contain(e =>
            e.ProviderName == "musicbrainz" && e.Value == "artist-from-tags");

        AssertAlbumRefreshQueued(albumId, "rg-from-tags");
    }

    [Test]
    public async Task Handle_ShouldQueueRefresh_WhenReusedAlbumGainsExternalId()
    {
        var artist = new MusicArtist { Title = "Justin Timberlake", SortTitle = "Justin Timberlake" };
        var album = new MusicAlbum { Title = "Justified", Artist = artist };
        _context.Medias.Add(artist);
        _context.Medias.Add(album);
        await _context.SaveChangesAsync();
        var albumId = album.Id;

        var fileId = Guid.NewGuid();
        _context.IndexedFiles.Add(CreateIndexedFile(fileId, "Cry Me a River", "Justified", "Justin Timberlake"));
        await _context.SaveChangesAsync();

        var returnedId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.MusicAlbum,
            LibraryId = _libraryId,
            IndexedFileIds = [fileId]
        }, CancellationToken.None);

        returnedId.Should().Be(albumId);

        var updated = await _context.Medias.OfType<MusicAlbum>()
            .Include(a => a.ExternalIds)
            .SingleAsync(a => a.Id == albumId);
        updated.ExternalIds.Should().Contain(e => e.ProviderName == "musicbrainz" && e.Value == "rg-from-search");

        AssertAlbumRefreshQueued(albumId, "rg-from-search");
    }

    [Test]
    public async Task Handle_ShouldAttachFileToVirtualTrack_WhenIsrcMatches()
    {
        var artist = new MusicArtist { Title = "Otis Redding", SortTitle = "Otis Redding" };
        var virtualAlbum = new MusicAlbum { Title = "Spotify", Artist = artist };
        var virtualTrack = new MusicTrack
        {
            Title = "(Sittin' On) The Dock of the Bay",
            SortTitle = "(Sittin' On) The Dock of the Bay",
            Album = virtualAlbum,
            Artist = artist
        };
        virtualTrack.ExternalIds.Add(new ExternalId { ProviderName = "isrc", Value = "USAT29900865" });
        virtualTrack.ExternalIds.Add(new ExternalId { ProviderName = "spotify", Value = "3zBhihYUHBmGd2bcQIobrF" });
        _context.Medias.AddRange(artist, virtualAlbum, virtualTrack);
        await _context.SaveChangesAsync();
        var virtualTrackId = virtualTrack.Id;

        var fileId = Guid.NewGuid();
        _tagReader.ReadTags(Arg.Any<string>(), Arg.Any<bool>()).Returns(new AudioTagData
        {
            Title = "(Sittin' On) The Dock of the Bay",
            Album = "The Dock of the Bay",
            AlbumArtists = ["Otis Redding"],
            Artists = ["Otis Redding"],
            TrackNumber = 1,
            Isrc = "USAT29900865"
        });

        _context.IndexedFiles.Add(CreateIndexedFile(fileId, "(Sittin' On) The Dock of the Bay", "The Dock of the Bay", "Otis Redding"));
        await _context.SaveChangesAsync();

        await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.MusicAlbum,
            LibraryId = _libraryId,
            IndexedFileIds = [fileId]
        }, CancellationToken.None);

        var tracks = await _context.Medias.OfType<MusicTrack>()
            .Include(t => t.IndexedFiles)
            .Include(t => t.Album)
            .ToListAsync();
        tracks.Should().ContainSingle();
        tracks[0].Id.Should().Be(virtualTrackId);
        tracks[0].IndexedFiles.Should().ContainSingle(f => f.Id == fileId);
        tracks[0].Album.Should().NotBeNull();
        tracks[0].Album!.Title.Should().Be("The Dock of the Bay");
    }

    [Test]
    public async Task Handle_ShouldAttachArtistMbid_WhenExistingArtistMatchedByName()
    {
        var artist = new MusicArtist { Title = "Justin Timberlake", SortTitle = "Justin Timberlake" };
        _context.Medias.Add(artist);
        await _context.SaveChangesAsync();

        var fileId = Guid.NewGuid();
        _tagReader.ReadTags(Arg.Any<string>(), Arg.Any<bool>()).Returns(new AudioTagData
        {
            Title = "Senorita",
            Album = "Justified",
            AlbumArtists = ["Justin Timberlake"],
            Artists = ["Justin Timberlake"],
            TrackNumber = 1,
            MusicBrainzReleaseGroupId = "rg-justified",
            MusicBrainzAlbumArtistId = "jt-mbid"
        });

        _context.IndexedFiles.Add(CreateIndexedFile(fileId, "Senorita", "Justified", "Justin Timberlake"));
        await _context.SaveChangesAsync();

        await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.MusicAlbum,
            LibraryId = _libraryId,
            IndexedFileIds = [fileId]
        }, CancellationToken.None);

        var updatedArtist = await _context.Medias.OfType<MusicArtist>()
            .Include(a => a.ExternalIds)
            .SingleAsync(a => a.Id == artist.Id);
        updatedArtist.ExternalIds.Should().ContainSingle(e =>
            e.ProviderName == "musicbrainz" && e.Value == "jt-mbid");
    }

    private void AssertAlbumRefreshQueued(Guid albumId, string externalId)
    {
        var calls = _sender.ReceivedCalls()
            .Select(c => c.GetArguments().ElementAtOrDefault(0))
            .OfType<CreateBackgroundTaskCommand>()
            .Select(c => c.Request)
            .OfType<RefreshMediaMetadatasCommand>()
            .ToList();

        calls.Should().Contain(r =>
            r.MediaId == albumId && r.MetadataProviderExternalId == externalId);
    }

    private IndexedFile CreateIndexedFile(Guid fileId, string title, string album, string artist) =>
        new()
        {
            Id = fileId,
            LibraryId = _libraryId,
            Path = $"/music/{album}/01 - {title}.flac",
            Name = $"01 - {title}.flac",
            Extension = ".flac",
            Hash = 1,
            Size = 1,
            MediaId = null,
            Identification = new MediaIdentification(title)
            {
                Title = title,
                AlbumName = album,
                ArtistName = artist,
                TrackNumber = 1
            }
        };
}

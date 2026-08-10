using K7.Server.Application.Common;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

[TestFixture]
public class MediaExternalIdResolverMusicTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private IMetadataProvider<ExternalMusicAlbumMetadata> _albumProvider = null!;
    private IMusicArtistMetadataProvider _artistProvider = null!;
    private IAudioTagReader _tagReader = null!;
    private MediaExternalIdResolver _sut = null!;
    private Library _library = null!;

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
        var libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Music",
            MediaType = LibraryMediaType.Music
        });
        _library = new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Music",
            MediaType = LibraryMediaType.Music,
            RootPath = "/music",
            MetadataProviderName = MetadataProviderNames.MusicBrainz,
            MetadataLanguage = "en",
            MetadataFallbackLanguage = "en"
        };
        _context.Libraries.Add(_library);
        _context.SaveChanges();

        _albumProvider = Substitute.For<IMetadataProvider<ExternalMusicAlbumMetadata>>();
        _albumProvider.ProviderName.Returns("musicbrainz");
        _albumProvider.SearchAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns("rg-search");

        _artistProvider = Substitute.For<IMusicArtistMetadataProvider>();
        _artistProvider.ProviderName.Returns("musicbrainz");
        _artistProvider.SearchByNameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalMusicArtistDetails { MusicBrainzArtistId = "artist-mbid" });

        _tagReader = Substitute.For<IAudioTagReader>();
        _tagReader.ReadTags(Arg.Any<string>(), Arg.Any<bool>()).Returns((AudioTagData?)null);

        var services = new ServiceCollection();
        services.AddKeyedSingleton("musicbrainz", _albumProvider);
        services.AddSingleton(_artistProvider);
        services.AddSingleton(_tagReader);
        _serviceProviderRoot = services.BuildServiceProvider();

        var identity = new MusicMetadataIdentityService(
            _serviceProviderRoot,
            Substitute.For<ILogger<MusicMetadataIdentityService>>());

        _sut = new MediaExternalIdResolver(
            _context,
            _serviceProviderRoot,
            identity,
            Substitute.For<ILogger<MediaExternalIdResolver>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
        _serviceProviderRoot.Dispose();
    }

    [Test]
    public async Task ResolveAsync_ShouldResolveMusicAlbum_ViaIdentitySearch()
    {
        var album = new MusicAlbum { Title = "Justified" };
        _context.Medias.Add(album);
        await _context.SaveChangesAsync();

        _context.IndexedFiles.Add(new IndexedFile
        {
            LibraryId = _library.Id,
            Path = "/music/Justified/01.flac",
            Name = "01.flac",
            Extension = ".flac",
            Hash = 1,
            Size = 1,
            MediaId = album.Id,
            Identification = new MediaIdentification("Cry Me a River")
            {
                AlbumName = "Justified",
                ArtistName = "Justin Timberlake"
            }
        });
        await _context.SaveChangesAsync();

        // Link file to album through a track (library linkage helper walks tracks).
        var track = new MusicTrack
        {
            Title = "Cry Me a River",
            AlbumId = album.Id,
            Album = album
        };
        _context.Medias.Add(track);
        await _context.SaveChangesAsync();

        var file = await _context.IndexedFiles.SingleAsync();
        file.MediaId = track.Id;
        await _context.SaveChangesAsync();

        var externalId = await _sut.ResolveAsync(album, _library);

        externalId.Should().NotBeNull();
        externalId!.ProviderName.Should().Be("musicbrainz");
        externalId.Value.Should().Be("rg-search");
        album.ExternalIds.Should().Contain(e => e.Value == "rg-search");
    }

    [Test]
    public async Task ResolveAsync_ShouldPreferTagReleaseGroup_ForMusicAlbum()
    {
        var album = new MusicAlbum { Title = "No Strings Attached" };
        _context.Medias.Add(album);
        var track = new MusicTrack { Title = "Bye Bye Bye", AlbumId = album.Id, Album = album };
        _context.Medias.Add(track);
        await _context.SaveChangesAsync();

        _context.IndexedFiles.Add(new IndexedFile
        {
            LibraryId = _library.Id,
            Path = "/music/NSA/01.flac",
            Name = "01.flac",
            Extension = ".flac",
            Hash = 2,
            Size = 1,
            MediaId = track.Id,
            Identification = new MediaIdentification("Bye Bye Bye")
            {
                AlbumName = "No Strings Attached",
                ArtistName = "*NSYNC"
            }
        });
        await _context.SaveChangesAsync();

        _tagReader.ReadTags("/music/NSA/01.flac", false).Returns(new AudioTagData
        {
            MusicBrainzReleaseGroupId = "rg-from-tags",
            MusicBrainzReleaseId = "release-from-tags"
        });

        var externalId = await _sut.ResolveAsync(album, _library);

        externalId!.Value.Should().Be("rg-from-tags");
        album.ExternalIds.Should().Contain(e => e.ProviderName == "musicbrainz-release" && e.Value == "release-from-tags");
        await _albumProvider.DidNotReceiveWithAnyArgs()
            .SearchAsync(default!, default, default, default);
    }

    [Test]
    public async Task ResolveAsync_ShouldResolveMusicArtist_ViaNameSearch()
    {
        var artist = new MusicArtist { Title = "Justin Timberlake" };
        _context.Medias.Add(artist);
        await _context.SaveChangesAsync();

        var externalId = await _sut.ResolveAsync(artist, _library);

        externalId.Should().NotBeNull();
        externalId!.ProviderName.Should().Be("musicbrainz");
        externalId.Value.Should().Be("artist-mbid");
        await _artistProvider.Received(1).SearchByNameAsync(
            "Justin Timberlake",
            "en",
            Arg.Any<CancellationToken>());
    }
}

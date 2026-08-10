using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class RefreshMediaMetadatasMusicArtistTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private IMetadataProvider<ExternalMusicAlbumMetadata> _albumProvider = null!;
    private IMusicArtistMetadataProvider _artistProvider = null!;
    private ISender _sender = null!;
    private RefreshMediaMetadatasCommandHandler _handler = null!;

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

        _albumProvider = Substitute.For<IMetadataProvider<ExternalMusicAlbumMetadata>>();
        _albumProvider.ProviderName.Returns("musicbrainz");

        _artistProvider = Substitute.For<IMusicArtistMetadataProvider>();
        _artistProvider.ProviderName.Returns("musicbrainz");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("musicbrainz", _albumProvider);
        _serviceProviderRoot = services.BuildServiceProvider();

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        _handler = new RefreshMediaMetadatasCommandHandler(
            _context,
            _serviceProviderRoot,
            _sender,
            [_artistProvider],
            Substitute.For<IMediaMetadataTagSyncService>(),
            new MetadataPictureDeletionService(
                _context,
                Substitute.For<ILogger<MetadataPictureDeletionService>>()),
            Substitute.For<ILogger<RefreshMediaMetadatasCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProviderRoot.Dispose();
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldSearchByNameAndUpsertMusicBrainz_WhenArtistHasNoExternalId()
    {
        var artist = new MusicArtist { Title = "Justin Timberlake" };
        _context.Medias.Add(artist);
        await _context.SaveChangesAsync();

        _artistProvider.SearchByNameAsync("Justin Timberlake", "en", Arg.Any<CancellationToken>())
            .Returns(new ExternalMusicArtistDetails
            {
                MusicBrainzArtistId = "jt-mbid",
                Country = "US"
            });

        await _handler.Handle(new RefreshMediaMetadatasCommand
        {
            MediaId = artist.Id,
            MetadataProviderExternalId = "",
            MetadataProviderName = "musicbrainz",
            Language = "en",
            FallbackLanguage = "en"
        }, CancellationToken.None);

        var updated = await _context.Medias.OfType<MusicArtist>()
            .Include(a => a.ExternalIds)
            .SingleAsync(a => a.Id == artist.Id);

        updated.Country.Should().Be("US");
        updated.ExternalIds.Should().ContainSingle(e =>
            e.ProviderName == "musicbrainz" && e.Value == "jt-mbid");
        await _artistProvider.Received(1).SearchByNameAsync(
            "Justin Timberlake",
            "en",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldMatchAlbumArtistByNormalizedName_WhenAssigningMbidFromMetadata()
    {
        var artist = new MusicArtist { Title = "*NSYNC" };
        var album = new MusicAlbum
        {
            Title = "No Strings Attached",
            ArtistId = artist.Id,
            Artist = artist
        };
        _context.Medias.Add(artist);
        _context.Medias.Add(album);
        await _context.SaveChangesAsync();

        // Re-link after SaveChanges so ArtistId is persisted correctly.
        album.ArtistId = artist.Id;
        await _context.SaveChangesAsync();

        _albumProvider.FetchMetadata("rg-nsa", "en", Arg.Any<CancellationToken>())
            .Returns(new ExternalMusicAlbumMetadata
            {
                Title = "No Strings Attached",
                Artists =
                [
                    new ExternalMusicArtistMetadata
                    {
                        Name = "Wrong Artist",
                        MusicBrainzArtistId = "wrong-mbid"
                    },
                    new ExternalMusicArtistMetadata
                    {
                        Name = "NSYNC",
                        MusicBrainzArtistId = "nsync-mbid"
                    }
                ]
            });

        _artistProvider.FetchByProviderIdAsync("nsync-mbid", "en", Arg.Any<CancellationToken>())
            .Returns(new ExternalMusicArtistDetails
            {
                MusicBrainzArtistId = "nsync-mbid",
                Country = "US"
            });

        await _handler.Handle(new RefreshMediaMetadatasCommand
        {
            MediaId = album.Id,
            MetadataProviderExternalId = "rg-nsa",
            MetadataProviderName = "musicbrainz",
            Language = "en",
            FallbackLanguage = "en"
        }, CancellationToken.None);

        var updatedArtist = await _context.Medias.OfType<MusicArtist>()
            .Include(a => a.ExternalIds)
            .SingleAsync(a => a.Id == artist.Id);

        updatedArtist.ExternalIds.Should().ContainSingle(e =>
            e.ProviderName == "musicbrainz" && e.Value == "nsync-mbid");
        updatedArtist.ExternalIds.Should().NotContain(e => e.Value == "wrong-mbid");
    }
}

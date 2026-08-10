using K7.Server.Application.Common;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

[TestFixture]
public class MusicMetadataIdentityServiceTests
{
    [Test]
    public async Task ResolveAlbumAsync_ShouldReturnTagReleaseGroup_WhenPresent()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var sut = new MusicMetadataIdentityService(
            services,
            Substitute.For<ILogger<MusicMetadataIdentityService>>());

        var match = await sut.ResolveAlbumAsync(
            new MediaIdentification("Album")
            {
                AlbumName = "Album",
                MusicBrainzReleaseGroupId = "rg-1",
                MusicBrainzAlbumArtistId = "artist-1",
                MusicBrainzReleaseId = "release-1"
            },
            MetadataProviderNames.MusicBrainz,
            "en",
            "en");

        match.Should().NotBeNull();
        match!.ExternalId.Should().Be("rg-1");
        match.ArtistMusicBrainzId.Should().Be("artist-1");
        match.PreferredReleaseId.Should().Be("release-1");
    }

    [Test]
    public async Task ResolveAlbumAsync_ShouldSearchProvider_WhenTagsMissing()
    {
        var provider = Substitute.For<IMetadataProvider<ExternalMusicAlbumMetadata>>();
        provider.ProviderName.Returns("musicbrainz");
        provider.SearchAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns("rg-from-search");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("musicbrainz", provider);
        await using var sp = services.BuildServiceProvider();

        var sut = new MusicMetadataIdentityService(
            sp,
            Substitute.For<ILogger<MusicMetadataIdentityService>>());

        var match = await sut.ResolveAlbumAsync(
            new MediaIdentification("Justified")
            {
                AlbumName = "Justified",
                ArtistName = "Justin Timberlake"
            },
            MetadataProviderNames.MusicBrainz,
            "en",
            "en");

        match.Should().NotBeNull();
        match!.ExternalId.Should().Be("rg-from-search");
        await provider.Received(1).SearchAsync(
            Arg.Is<MediaIdentification>(i => i.AlbumName == "Justified"),
            "en",
            "en",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ResolveArtistIdAsync_ShouldReturnKnownId_WithoutSearch()
    {
        var artistProvider = Substitute.For<IMusicArtistMetadataProvider>();
        var services = new ServiceCollection();
        services.AddSingleton(artistProvider);
        await using var sp = services.BuildServiceProvider();

        var sut = new MusicMetadataIdentityService(
            sp,
            Substitute.For<ILogger<MusicMetadataIdentityService>>());

        var id = await sut.ResolveArtistIdAsync("Justin Timberlake", "known-mbid", "en");

        id.Should().Be("known-mbid");
        await artistProvider.DidNotReceiveWithAnyArgs()
            .SearchByNameAsync(default!, default!, default);
    }

    [Test]
    public async Task ResolveArtistIdAsync_ShouldSearchByName_WhenIdMissing()
    {
        var artistProvider = Substitute.For<IMusicArtistMetadataProvider>();
        artistProvider.ProviderName.Returns("musicbrainz");
        artistProvider.SearchByNameAsync("Justin Timberlake", "en", Arg.Any<CancellationToken>())
            .Returns(new ExternalMusicArtistDetails { MusicBrainzArtistId = "jt-mbid" });

        var services = new ServiceCollection();
        services.AddSingleton(artistProvider);
        await using var sp = services.BuildServiceProvider();

        var sut = new MusicMetadataIdentityService(
            sp,
            Substitute.For<ILogger<MusicMetadataIdentityService>>());

        var id = await sut.ResolveArtistIdAsync("Justin Timberlake", null, "en");

        id.Should().Be("jt-mbid");
    }

    [Test]
    public async Task ResolveAlbumAsync_ShouldUseMusicBrainz_WhenLibraryProviderIsAuto()
    {
        var provider = Substitute.For<IMetadataProvider<ExternalMusicAlbumMetadata>>();
        provider.SearchAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns("rg-auto");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("musicbrainz", provider);
        await using var sp = services.BuildServiceProvider();

        var sut = new MusicMetadataIdentityService(
            sp,
            Substitute.For<ILogger<MusicMetadataIdentityService>>());

        var match = await sut.ResolveAlbumAsync(
            new MediaIdentification("Album") { AlbumName = "Album", ArtistName = "Artist" },
            MetadataProviderNames.Auto,
            "en",
            "en");

        match.Should().NotBeNull();
        match!.ProviderName.Should().Be(MetadataProviderNames.MusicBrainz);
        match.ExternalId.Should().Be("rg-auto");
    }

    [Test]
    public async Task ResolveAlbumAsync_ShouldReturnNull_WhenSearchMisses()
    {
        var provider = Substitute.For<IMetadataProvider<ExternalMusicAlbumMetadata>>();
        provider.SearchAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var services = new ServiceCollection();
        services.AddKeyedSingleton("musicbrainz", provider);
        await using var sp = services.BuildServiceProvider();

        var sut = new MusicMetadataIdentityService(
            sp,
            Substitute.For<ILogger<MusicMetadataIdentityService>>());

        var match = await sut.ResolveAlbumAsync(
            new MediaIdentification("Unknown") { AlbumName = "Unknown" },
            MetadataProviderNames.MusicBrainz,
            "en",
            "en");

        match.Should().BeNull();
    }

    [Test]
    public async Task ResolveAlbumAsync_ShouldPassArtistMbidIntoSearch_WhenPresent()
    {
        var provider = Substitute.For<IMetadataProvider<ExternalMusicAlbumMetadata>>();
        provider.SearchAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns("rg-arid");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("musicbrainz", provider);
        await using var sp = services.BuildServiceProvider();

        var sut = new MusicMetadataIdentityService(
            sp,
            Substitute.For<ILogger<MusicMetadataIdentityService>>());

        await sut.ResolveAlbumAsync(
            new MediaIdentification("Album")
            {
                AlbumName = "Album",
                ArtistName = "*NSYNC",
                MusicBrainzAlbumArtistId = "artist-mbid"
            },
            MetadataProviderNames.MusicBrainz,
            "en",
            "en");

        await provider.Received(1).SearchAsync(
            Arg.Is<MediaIdentification>(i =>
                i.MusicBrainzAlbumArtistId == "artist-mbid"
                && i.AlbumName == "Album"),
            "en",
            "en",
            Arg.Any<CancellationToken>());
    }
}

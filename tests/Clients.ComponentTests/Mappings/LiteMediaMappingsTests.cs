using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Home;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Mappings;

[TestFixture]
public class LiteMediaMappingsTests
{
    [Test]
    public void ToCardViewModel_ShouldUseArtistName_ForMusicAlbum()
    {
        var item = new LiteMusicAlbumDto
        {
            Id = Guid.NewGuid(),
            Title = "Discovery",
            ArtistName = "Daft Punk",
            ReleaseDate = new DateOnly(2001, 3, 12)
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient, n => $"Season {n}");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Discovery");
        result.AdditionalInformations.Should().Be("Daft Punk");
        result.ReleaseYear.Should().Be(2001);
    }

    [Test]
    public void ToCardViewModel_ShouldUseArtistName_ForMusicTrack()
    {
        var item = new LiteMusicTrackDto
        {
            Id = Guid.NewGuid(),
            AlbumId = Guid.NewGuid(),
            Title = "One More Time",
            ArtistName = "Daft Punk",
            ReleaseDate = new DateOnly(2000, 11, 13)
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient, n => $"Season {n}");

        result.Should().NotBeNull();
        result!.Title.Should().Be("One More Time");
        result.AdditionalInformations.Should().Be("Daft Punk");
    }

    [Test]
    public void ToCardViewModel_ShouldUseReleaseYear_WhenMusicAlbumHasNoArtist()
    {
        var item = new LiteMusicAlbumDto
        {
            Id = Guid.NewGuid(),
            Title = "Discovery",
            ReleaseDate = new DateOnly(2001, 3, 12)
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient, n => $"Season {n}");

        result.Should().NotBeNull();
        result!.AdditionalInformations.Should().Be("2001");
    }

    [Test]
    public void ToCardViewModel_ShouldUseArtistAdditionalInfo_ForHomeFeedMusicAlbum()
    {
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Discovery",
            MediaType = MediaType.MusicAlbum,
            NavigationTarget = "/music/albums/1",
            AdditionalInfo = "Daft Punk",
            ReleaseDate = new DateOnly(2001, 3, 12)
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient);

        result.AdditionalInformations.Should().Be("Daft Punk");
    }

    [Test]
    public void ToCardViewModel_ShouldUseReleaseYear_WhenAdditionalInfoIsNull()
    {
        // Arrange
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Inception",
            MediaType = MediaType.Movie,
            NavigationTarget = "/movies/1",
            AdditionalInfo = null,
            ReleaseDate = new DateOnly(2010, 7, 16)
        };
        var apiClient = Substitute.For<IK7ServerService>();

        // Act
        var result = item.ToCardViewModel(apiClient);

        // Assert
        result.AdditionalInformations.Should().Be("2010");
    }

    [Test]
    public void ToCardViewModel_ShouldPreferAdditionalInfo_WhenPresent()
    {
        // Arrange
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Inception",
            MediaType = MediaType.Movie,
            NavigationTarget = "/movies/1",
            AdditionalInfo = "2h 28m left",
            ReleaseDate = new DateOnly(2010, 7, 16)
        };
        var apiClient = Substitute.For<IK7ServerService>();

        // Act
        var result = item.ToCardViewModel(apiClient);

        // Assert
        result.AdditionalInformations.Should().Be("2h 28m left");
    }

    [Test]
    public void ToCardViewModel_ShouldUseSeriePoster_WhenEpisodeHasNoStill()
    {
        var posterUri = new Uri("/api/pictures/poster.jpg", UriKind.Relative);
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Breaking Bad",
            MediaType = MediaType.SerieEpisode,
            NavigationTarget = "/series/1/seasons/1#ep-2",
            AdditionalInfo = "S01E02",
            Pictures =
            [
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Poster,
                    Uri = posterUri
                }
            ]
        };
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));

        var result = item.ToCardViewModel(apiClient);

        result.PictureUrl.Should().NotBeNullOrEmpty();
        result.PictureUrl.Should().StartWith("https://localhost/api/pictures/poster.jpg");
        result.PictureUrl.Should().Contain("v=");
    }

    [Test]
    public void ToCardViewModel_ShouldUseSeasonPosterAndStillBackdrop_WhenMergedEpisodePictures()
    {
        var seasonPosterUri = new Uri("/api/pictures/season-poster.jpg", UriKind.Relative);
        var seriePosterUri = new Uri("/api/pictures/serie-poster.jpg", UriKind.Relative);
        var stillUri = new Uri("/api/pictures/still.jpg", UriKind.Relative);
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Breaking Bad",
            MediaType = MediaType.SerieEpisode,
            NavigationTarget = "/series/1/seasons/1#ep-2",
            AdditionalInfo = "S01E02",
            Pictures =
            [
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Poster,
                    Uri = seasonPosterUri
                },
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Poster,
                    Uri = seriePosterUri
                },
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Still,
                    Uri = stillUri
                }
            ]
        };
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));

        var result = item.ToCardViewModel(apiClient);

        result.PictureUrl.Should().Contain("season-poster.jpg");
        result.PictureUrl.Should().NotContain("still.jpg");
        result.BackdropUrl.Should().Contain("still.jpg");
        result.SoftHeroBackdrop.Should().BeFalse();
    }

    [Test]
    public void ToCardViewModel_ShouldSoftenHeroBackdrop_WhenEpisodeStillIsBelowHd()
    {
        var stillUri = new Uri("/api/pictures/still.jpg", UriKind.Relative);
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Breaking Bad",
            MediaType = MediaType.SerieEpisode,
            NavigationTarget = "/series/1/seasons/1#ep-2",
            Pictures =
            [
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Still,
                    Uri = stillUri,
                    OriginalWidth = 640,
                    OriginalHeight = 360
                }
            ]
        };
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));

        var result = item.ToCardViewModel(apiClient);

        result.BackdropUrl.Should().Contain("still.jpg");
        result.SoftHeroBackdrop.Should().BeTrue();
    }

    [Test]
    public void ToCardViewModel_ShouldKeepHeroBackdropSharp_WhenEpisodeStillIsHd()
    {
        var stillUri = new Uri("/api/pictures/still.jpg", UriKind.Relative);
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Breaking Bad",
            MediaType = MediaType.SerieEpisode,
            NavigationTarget = "/series/1/seasons/1#ep-2",
            Pictures =
            [
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Still,
                    Uri = stillUri,
                    OriginalWidth = 1920,
                    OriginalHeight = 1080
                }
            ]
        };
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));

        var result = item.ToCardViewModel(apiClient);

        result.BackdropUrl.Should().Contain("still.jpg");
        result.SoftHeroBackdrop.Should().BeFalse();
    }

    [Test]
    public void ToCardViewModel_ShouldNotUseStill_ForEpisodeCardTile()
    {
        var stillUri = new Uri("/api/pictures/still.jpg", UriKind.Relative);
        var posterUri = new Uri("/api/pictures/poster.jpg", UriKind.Relative);
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Breaking Bad",
            MediaType = MediaType.SerieEpisode,
            NavigationTarget = "/series/1/seasons/1#ep-2",
            Pictures =
            [
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Still,
                    Uri = stillUri
                },
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Poster,
                    Uri = posterUri
                }
            ]
        };
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));

        var result = item.ToCardViewModel(apiClient);

        result.PictureUrl.Should().Contain("poster.jpg");
        result.BackdropUrl.Should().Contain("still.jpg");
    }

    [Test]
    public void ToCardViewModel_ShouldUseAlbumCover_ForMusicHeroBackdrop()
    {
        var coverUri = new Uri("/api/pictures/album-cover.jpg", UriKind.Relative);
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Discovery",
            MediaType = MediaType.MusicAlbum,
            NavigationTarget = "/music/albums/1",
            Pictures =
            [
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Cover,
                    Uri = coverUri
                }
            ]
        };
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));

        var result = item.ToCardViewModel(apiClient);

        result.PictureUrl.Should().NotBeNullOrEmpty();
        result.PictureUrl.Should().Contain("album-cover.jpg");
        result.BackdropUrl.Should().NotBeNullOrEmpty();
        result.BackdropUrl.Should().Contain("album-cover.jpg");
        result.ResolveHeroBackdropUrl().Should().Be(result.BackdropUrl);
        result.SoftHeroBackdrop.Should().BeTrue();
    }

    [Test]
    public void WithHeroDetailsFromMedia_ShouldUseVideoFileDuration_ForMovie()
    {
        var source = CreateHeroSource(MediaType.Movie);
        var media = new MovieDto
        {
            Title = "Dune",
            IndexedFiles =
            [
                new IndexedFileDto
                {
                    Id = Guid.NewGuid(),
                    LibraryId = Guid.NewGuid(),
                    Name = "dune",
                    Extension = ".mkv",
                    Path = "dune.mkv",
                    Hash = 1,
                    Size = 1,
                    FileMetadata = new VideoFileMetadataDto
                    {
                        Container = "mkv",
                        VideoBitrate = 1,
                        VideoResolution = VideoResolutionIdentifier._1080p,
                        Duration = TimeSpan.FromMinutes(125)
                    }
                }
            ]
        };

        var result = source.WithHeroDetailsFromMedia(media, Substitute.For<IK7ServerService>());

        result.RuntimeMinutes.Should().Be(125);
    }

    [Test]
    public void WithHeroDetailsFromMedia_ShouldUseTypicalEpisodeRuntime_ForSerie()
    {
        var source = CreateHeroSource(MediaType.Serie);
        var media = new SerieDto
        {
            Title = "Breaking Bad",
            Runtime = 47
        };

        var result = source.WithHeroDetailsFromMedia(media, Substitute.For<IK7ServerService>());

        result.RuntimeMinutes.Should().Be(47);
    }

    [Test]
    public void WithHeroDetailsFromMedia_ShouldUseEpisodeRuntime_ForSerieEpisode()
    {
        var source = CreateHeroSource(MediaType.SerieEpisode);
        var media = new SerieEpisodeDto
        {
            Title = "Pilot",
            Runtime = 58
        };

        var result = source.WithHeroDetailsFromMedia(media, Substitute.For<IK7ServerService>());

        result.RuntimeMinutes.Should().Be(58);
    }

    private static MediaCardViewModel CreateHeroSource(MediaType mediaType) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Title = "Title",
        Kind = MediaCardKind.Poster,
        MediaType = mediaType
    };
}

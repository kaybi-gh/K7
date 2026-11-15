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
        result.SubtitleHref.Should().BeNull();
        result.ReleaseYear.Should().Be(2001);
    }

    [Test]
    public void ToCardViewModel_ShouldLinkArtist_WhenMusicAlbumHasArtistId()
    {
        var artistId = Guid.NewGuid();
        var item = new LiteMusicAlbumDto
        {
            Id = Guid.NewGuid(),
            Title = "Discovery",
            ArtistId = artistId,
            ArtistName = "Daft Punk"
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient, n => $"Season {n}");

        result!.SubtitleHref.Should().Be($"/music/artists/{artistId}");
        result.RelatedLinks.Should().ContainSingle(link =>
            link.Kind == MediaCardRelatedKind.Artist && link.Href == $"/music/artists/{artistId}");
    }

    [Test]
    public void ToCardViewModel_ShouldLinkArtistAndAlbum_ForMusicTrack()
    {
        var artistId = Guid.NewGuid();
        var albumId = Guid.NewGuid();
        var item = new LiteMusicTrackDto
        {
            Id = Guid.NewGuid(),
            AlbumId = albumId,
            ArtistId = artistId,
            Title = "One More Time",
            ArtistName = "Daft Punk"
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient, n => $"Season {n}");

        result!.SubtitleHref.Should().Be($"/music/artists/{artistId}");
        result.RelatedLinks.Select(link => link.Kind).Should().Equal(
            MediaCardRelatedKind.Album,
            MediaCardRelatedKind.Artist);
    }

    [Test]
    public void ToCardViewModel_ShouldLinkSerie_WhenEpisodeSubtitleIsSerieTitle()
    {
        var serieId = Guid.NewGuid();
        var item = new LiteSerieEpisodeDto
        {
            Id = Guid.NewGuid(),
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            SerieId = serieId,
            SerieTitle = "Breaking Bad"
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient, n => $"Season {n}", preferEpisodeStill: true);

        result!.AdditionalInformations.Should().Be("Breaking Bad");
        result.SubtitleHref.Should().Be($"/series/{serieId}");
        result.RelatedLinks.Select(link => link.Kind).Should().Equal(
            MediaCardRelatedKind.Series,
            MediaCardRelatedKind.Season);
    }

    [Test]
    public void ToCardViewModel_ShouldKeepEpisodeCodeAsText_WhenSubtitleIsNotSerieTitle()
    {
        var serieId = Guid.NewGuid();
        var item = new LiteSerieEpisodeDto
        {
            Id = Guid.NewGuid(),
            Title = "Pilot",
            EpisodeNumber = 2,
            SeasonNumber = 1,
            SerieId = serieId,
            SerieTitle = "Breaking Bad"
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient, n => $"Season {n}");

        result!.AdditionalInformations.Should().Be("S01E02");
        result.SubtitleHref.Should().BeNull();
        result.TitleHref.Should().BeNull();
        result.RelatedLinks.Should().Contain(link => link.Kind == MediaCardRelatedKind.Series);
    }

    [Test]
    public void ToCardViewModel_ShouldLinkSerie_WhenSeasonTitleIsSerieTitle()
    {
        var serieId = Guid.NewGuid();
        var item = new LiteSerieSeasonDto
        {
            Id = Guid.NewGuid(),
            Title = "Breaking Bad",
            SeasonNumber = 2,
            SerieId = serieId,
            SerieTitle = "Breaking Bad"
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient, n => $"Season {n}");

        result!.Title.Should().Be("Breaking Bad");
        result.TitleHref.Should().Be($"/series/{serieId}");
    }

    [Test]
    public void ToCardViewModel_ShouldKeepSeasonTitleAsText_WhenItIsNotSerieTitle()
    {
        var item = new LiteSerieSeasonDto
        {
            Id = Guid.NewGuid(),
            Title = "Season 2",
            SeasonNumber = 2,
            SerieId = Guid.NewGuid(),
            SerieTitle = "Breaking Bad"
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient, n => $"Season {n}");

        result!.TitleHref.Should().BeNull();
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
        result.SubtitleHref.Should().BeNull();
    }

    [Test]
    public void ToCardViewModel_ShouldLinkArtist_WhenHomeFeedAlbumHasArtistId()
    {
        var artistId = Guid.NewGuid();
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Discovery",
            MediaType = MediaType.MusicAlbum,
            NavigationTarget = "/music/albums/1",
            AdditionalInfo = "Daft Punk",
            RelatedArtistId = artistId
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient);

        result.SubtitleHref.Should().Be($"/music/artists/{artistId}");
        result.RelatedLinks.Should().ContainSingle(link => link.Kind == MediaCardRelatedKind.Artist);
    }

    [Test]
    public void ToCardViewModel_ShouldUseYear_WhenHomeFeedSerieHasNoCountSubtitle()
    {
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Breaking Bad",
            MediaType = MediaType.Serie,
            NavigationTarget = "/series/1",
            ReleaseDate = new DateOnly(2008, 1, 20)
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient);

        result.AdditionalInformations.Should().Be("2008");
        result.SubtitleHref.Should().BeNull();
        result.RelatedLinks.Should().BeEmpty();
    }

    [Test]
    public void ToCardViewModel_ShouldLeaveSeasonSubtitleEmpty_WhenHomeFeedSeasonDropsEpisodeCount()
    {
        var serieId = Guid.NewGuid();
        var item = new HomeFeedItemDto
        {
            Id = serieId,
            Title = "Breaking Bad",
            MediaType = MediaType.SerieSeason,
            NavigationTarget = $"/series/{serieId}/seasons/2",
            RelatedSerieId = serieId,
            RelatedSeasonNumber = 2,
            ReleaseDate = new DateOnly(2009, 3, 8)
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient);

        result.AdditionalInformations.Should().BeNull();
        result.TitleHref.Should().Be($"/series/{serieId}");
        result.SeasonNumber.Should().Be(2);
        result.RelatedLinks.Select(link => link.Href).Should().Equal(
            $"/series/{serieId}",
            $"/series/{serieId}/seasons/2");
    }

    [Test]
    public void ToCardViewModel_ShouldKeepEpisodeCode_WhenHomeFeedEpisodeLinksToSerie()
    {
        var serieId = Guid.NewGuid();
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Breaking Bad",
            MediaType = MediaType.SerieEpisode,
            NavigationTarget = $"/series/{serieId}/seasons/1#ep-2",
            AdditionalInfo = "S01E02",
            RelatedSerieId = serieId,
            RelatedSeasonNumber = 1
        };
        var apiClient = Substitute.For<IK7ServerService>();

        var result = item.ToCardViewModel(apiClient);

        result.AdditionalInformations.Should().Be("S01E02");
        result.SubtitleHref.Should().BeNull();
        result.TitleHref.Should().Be($"/series/{serieId}");
        result.RelatedLinks.Select(link => link.Kind).Should().Equal(
            MediaCardRelatedKind.Series,
            MediaCardRelatedKind.Season);
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
        result.PictureUrl.Should().NotContain("v=");
    }

    [Test]
    public void ToCardViewModel_ShouldShareStableMediumBackdropUrl_WhenMovieHasBackdrop()
    {
        var backdropUri = new Uri("/api/metadata-pictures/backdrop.jpg", UriKind.Relative);
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Dune",
            MediaType = MediaType.Movie,
            NavigationTarget = "/movies/1",
            Pictures =
            [
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Backdrop,
                    Uri = backdropUri,
                    OriginalWidth = 3840,
                    OriginalHeight = 2160
                }
            ]
        };
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));

        var result = item.ToCardViewModel(apiClient);

        result.BackdropUrl.Should().Be("https://localhost/api/metadata-pictures/backdrop.jpg?size=Medium");
        result.BackdropUrl.Should().NotContain("v=");
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

        result.BackdropUrl.Should().Be("https://localhost/api/pictures/still.jpg");
        result.BackdropUrl.Should().NotContain("size=");
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
        result.BackdropUrl.Should().NotContain("size=");
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

    [Test]
    public void ToCardViewModel_ShouldCarryTheCardDetails_WhenTheBrowseQuerySentThem()
    {
        var item = new LiteMovieDto
        {
            Id = Guid.NewGuid(),
            Title = "Dune",
            ReleaseDate = new DateOnly(2021, 9, 15),
            Details = new LiteMediaCardDetailsDto
            {
                Genre = "Science Fiction",
                ContentRating = "PG-13",
                RuntimeMinutes = 155,
                Rating = 8.0
            }
        };

        var result = item.ToCardViewModel(Substitute.For<IK7ServerService>(), n => $"Season {n}");

        result!.Genres.Should().Equal("Science Fiction");
        result.ContentRating.Should().Be("PG-13");
        result.RuntimeMinutes.Should().Be(155);
        result.Rating.Should().Be(8.0);
        result.ReleaseYear.Should().Be(2021);
    }

    [Test]
    public void ToCardViewModel_ShouldLeaveCardDetailsEmpty_WhenTheBrowseQueryDidNotAsk()
    {
        var item = new LiteMovieDto { Id = Guid.NewGuid(), Title = "Dune" };

        var result = item.ToCardViewModel(Substitute.For<IK7ServerService>(), n => $"Season {n}");

        result!.Genres.Should().BeNull();
        result.RuntimeMinutes.Should().BeNull();
        result.Rating.Should().BeNull();
    }

    private static IK7ServerService ApiClient()
    {
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));
        return apiClient;
    }

    private static MetadataPictureDto Picture(MetadataPictureType type, string path) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Uri = new Uri(path, UriKind.Relative)
    };

    [Test]
    public void ToCardViewModel_ShouldGiveALandscapePicture_WhenHomeFeedMovieHasABackdrop()
    {
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Dune",
            MediaType = MediaType.Movie,
            NavigationTarget = "/movies/1",
            Pictures =
            [
                Picture(MetadataPictureType.Poster, "/pictures/poster.jpg"),
                Picture(MetadataPictureType.Backdrop, "/pictures/backdrop.jpg")
            ]
        };

        var result = item.ToCardViewModel(ApiClient());

        result.LandscapeUrl.Should().Contain("backdrop");
        result.PictureUrl.Should().Contain("poster");
        result.LogoUrl.Should().BeNull();
    }

    [Test]
    public void ToCardViewModel_ShouldUseTheStillAsLandscapePicture_ForHomeFeedEpisode()
    {
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Pilot",
            MediaType = MediaType.SerieEpisode,
            NavigationTarget = "/series/1/seasons/1#ep-1",
            Pictures =
            [
                Picture(MetadataPictureType.Poster, "/pictures/poster.jpg"),
                Picture(MetadataPictureType.Still, "/pictures/still.jpg"),
                Picture(MetadataPictureType.Logo, "/pictures/logo.png")
            ]
        };

        var result = item.ToCardViewModel(ApiClient());

        result.LogoUrl.Should().Contain("logo");
        result.LandscapeUrl.Should().Contain("still");
    }

    [Test]
    public void ToCardViewModel_ShouldLeaveLandscapePictureEmpty_WhenHomeFeedItemOnlyHasACoverOrPoster()
    {
        var album = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Discovery",
            MediaType = MediaType.MusicAlbum,
            NavigationTarget = "/music/albums/1",
            Pictures = [Picture(MetadataPictureType.Cover, "/pictures/cover.jpg")]
        };
        var movie = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Dune",
            MediaType = MediaType.Movie,
            NavigationTarget = "/movies/1",
            Pictures = [Picture(MetadataPictureType.Poster, "/pictures/poster.jpg")]
        };

        album.ToCardViewModel(ApiClient()).LandscapeUrl.Should().BeNull();
        movie.ToCardViewModel(ApiClient()).LandscapeUrl.Should().BeNull();
    }
}

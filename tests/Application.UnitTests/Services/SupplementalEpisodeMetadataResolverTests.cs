using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;

namespace K7.Server.Application.UnitTests.Services;

public class SupplementalEpisodeMetadataResolverTests
{
    [Test]
    public async Task TryFetchTmdbEpisodeMetadataAsync_ShouldReturnMetadata_WhenPrimaryProviderIsTvdb()
    {
        var tvdb = new StubSerieMetadataProvider("tvdb");
        var tmdb = new StubSerieMetadataProvider("tmdb", stillUrl: "https://tmdb.example/still.jpg", tmdbEpisodeId: "42", episodeRating: 8.2);
        var serie = new Serie
        {
            Title = "Test",
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = "1396" }]
        };

        var result = await SupplementalEpisodeMetadataResolver.TryFetchTmdbEpisodeMetadataAsync(
            tvdb,
            tmdb,
            serie,
            seasonNumber: 1,
            episodeNumber: 1,
            language: "en",
            fallbackLanguage: "en",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.StillImageUrl.Should().Be("https://tmdb.example/still.jpg");
        result.ExternalIds.Should().ContainSingle(e => e.ProviderName == "tmdb" && e.Value == "42");
        result.Ratings.Should().ContainSingle(r => r.MetadataProvider == MetadataProvider.TMDb && r.Value == 8.2);
    }

    [Test]
    public async Task TryFetchTmdbEpisodeMetadataAsync_ShouldReturnNull_WhenPrimaryProviderIsNotTvdb()
    {
        var tmdb = new StubSerieMetadataProvider("tmdb", stillUrl: "https://tmdb.example/still.jpg");
        var other = new StubSerieMetadataProvider("tmdb");
        var serie = new Serie
        {
            Title = "Test",
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = "1396" }]
        };

        var result = await SupplementalEpisodeMetadataResolver.TryFetchTmdbEpisodeMetadataAsync(
            other,
            tmdb,
            serie,
            seasonNumber: 1,
            episodeNumber: 1,
            language: "en",
            fallbackLanguage: "en",
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task TryFetchTmdbSerieMetadataAsync_ShouldReturnRatings_WhenPrimaryProviderIsTvdb()
    {
        var tvdb = new StubSerieMetadataProvider("tvdb");
        var tmdb = new StubSerieMetadataProvider("tmdb", serieRating: 7.9);
        var serie = new Serie
        {
            Title = "Test",
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = "1396" }]
        };

        var result = await SupplementalEpisodeMetadataResolver.TryFetchTmdbSerieMetadataAsync(
            tvdb,
            tmdb,
            serie,
            language: "en",
            fallbackLanguage: "en",
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Ratings.Should().ContainSingle(r => r.MetadataProvider == MetadataProvider.TMDb && r.Value == 7.9);
    }

    [Test]
    public void MergeSupplementalExternalIds_ShouldAddMissingProviders_WithoutReplacingExisting()
    {
        var episode = new SerieEpisode
        {
            EpisodeNumber = 1,
            ExternalIds =
            [
                new ExternalId { ProviderName = "tvdb", Value = "123" },
                new ExternalId { ProviderName = "imdb", Value = "tt0001" }
            ]
        };

        SupplementalEpisodeMetadataResolver.MergeSupplementalExternalIds(
            episode,
            [
                new ExternalId { ProviderName = "tmdb", Value = "42" },
                new ExternalId { ProviderName = "imdb", Value = "tt9999" }
            ]);

        episode.ExternalIds.Should().HaveCount(3);
        episode.ExternalIds.Should().Contain(e => e.ProviderName == "tvdb" && e.Value == "123");
        episode.ExternalIds.Should().Contain(e => e.ProviderName == "imdb" && e.Value == "tt0001");
        episode.ExternalIds.Should().Contain(e => e.ProviderName == "tmdb" && e.Value == "42");
    }

    [Test]
    public void MergeMetadataProviderRatings_ShouldUpsertByProvider()
    {
        var episode = new SerieEpisode { EpisodeNumber = 1 };
        episode.Ratings.Add(new MetadataProviderRating
        {
            MetadataProvider = MetadataProvider.TMDb,
            Value = 7.0,
            MinimumValue = 0,
            MaximumValue = 10,
            RatingCount = 10
        });

        SupplementalEpisodeMetadataResolver.MergeMetadataProviderRatings(
            episode,
            [
                new MetadataProviderRating
                {
                    MetadataProvider = MetadataProvider.TMDb,
                    Value = 8.5,
                    MinimumValue = 0,
                    MaximumValue = 10,
                    RatingCount = 100
                }
            ]);

        episode.Ratings.OfType<MetadataProviderRating>().Should().ContainSingle();
        var rating = episode.Ratings.OfType<MetadataProviderRating>().Single();
        rating.Value.Should().Be(8.5);
        rating.RatingCount.Should().Be(100);
    }

    private sealed class StubSerieMetadataProvider(
        string providerName,
        string? stillUrl = null,
        string? tmdbEpisodeId = null,
        double? serieRating = null,
        double? episodeRating = null)
        : ISerieMetadataProvider
    {
        public string ProviderName => providerName;

        public Task<string?> SearchSerieAsync(MediaIdentification identification, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<ExternalSerieMetadata> FetchSerieMetadataAsync(
            string providerId,
            string language,
            CancellationToken cancellationToken = default,
            string? fallbackLanguage = null) =>
            Task.FromResult(new ExternalSerieMetadata
            {
                Title = "Test",
                Ratings = serieRating is double value
                    ?
                    [
                        new MetadataProviderRating
                        {
                            MetadataProvider = MetadataProvider.TMDb,
                            Value = value,
                            MinimumValue = 0,
                            MaximumValue = 10,
                            RatingCount = 50
                        }
                    ]
                    : []
            });

        public Task<ExternalSeasonMetadata> FetchSeasonMetadataAsync(
            string providerId,
            int seasonNumber,
            string language,
            CancellationToken cancellationToken = default,
            string? fallbackLanguage = null) =>
            Task.FromResult(new ExternalSeasonMetadata());

        public Task<ExternalEpisodeMetadata> FetchEpisodeMetadataAsync(
            string providerId,
            int seasonNumber,
            int episodeNumber,
            string language,
            CancellationToken cancellationToken = default,
            string? fallbackLanguage = null) =>
            Task.FromResult(new ExternalEpisodeMetadata
            {
                SeasonNumber = seasonNumber,
                EpisodeNumber = episodeNumber,
                StillImageUrl = stillUrl,
                ExternalIds = tmdbEpisodeId is null
                    ? []
                    : [new ExternalId { ProviderName = "tmdb", Value = tmdbEpisodeId }],
                Ratings = episodeRating is double value
                    ?
                    [
                        new MetadataProviderRating
                        {
                            MetadataProvider = MetadataProvider.TMDb,
                            Value = value,
                            MinimumValue = 0,
                            MaximumValue = 10,
                            RatingCount = 25
                        }
                    ]
                    : []
            });

        public Task<(int Season, int Episode)?> ResolveAbsoluteEpisodeAsync(
            string providerId,
            int absoluteNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(int Season, int Episode)?>(null);
    }
}

using K7.Server.Application.Common.Services;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Application.UnitTests.Services;

public class SupplementalEpisodeMetadataResolverTests
{
    [Test]
    public async Task TryFetchTmdbEpisodeMetadataAsync_ShouldReturnMetadata_WhenPrimaryProviderIsTvdb()
    {
        var tvdb = new StubSerieMetadataProvider("tvdb");
        var tmdb = new StubSerieMetadataProvider("tmdb", stillUrl: "https://tmdb.example/still.jpg", tmdbEpisodeId: "42");
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

    private sealed class StubSerieMetadataProvider(string providerName, string? stillUrl = null, string? tmdbEpisodeId = null)
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
            Task.FromResult(new ExternalSerieMetadata { Title = "Test" });

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
                    : [new ExternalId { ProviderName = "tmdb", Value = tmdbEpisodeId }]
            });

        public Task<(int Season, int Episode)?> ResolveAbsoluteEpisodeAsync(
            string providerId,
            int absoluteNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(int Season, int Episode)?>(null);
    }
}

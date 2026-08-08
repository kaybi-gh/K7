using System.Net;
using System.Text;
using K7.Server.Application.Services;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Metadata.Tvdb;

[TestFixture]
public class TvdbSerieMetadataProviderEpisodeLookupTests
{
    [Test]
    public async Task FetchEpisodeMetadataAsync_ShouldUseAbsoluteOrder_WhenMissingFromDefault()
    {
        var handler = new TvdbStubHttpMessageHandler();
        using var httpClient = new HttpClient(handler);

        var rateLimiter = new OutboundRateLimiter();
        var cooldownStore = new MetadataProviderCooldownStore();
        var auth = new TvdbAuthenticationService(
            httpClient,
            rateLimiter,
            cooldownStore,
            Substitute.For<ILogger<TvdbAuthenticationService>>());
        var apiClient = new TvdbApiClient(
            httpClient,
            auth,
            rateLimiter,
            cooldownStore,
            Substitute.For<ILogger<TvdbApiClient>>());
        var provider = new TvdbSerieMetadataProvider(
            apiClient,
            Substitute.For<ILogger<TvdbSerieMetadataProvider>>());

        var metadata = await provider.FetchEpisodeMetadataAsync(
            "337018",
            seasonNumber: 1,
            episodeNumber: 11,
            language: "en",
            cancellationToken: CancellationToken.None,
            fallbackLanguage: "en");

        metadata.Title.Should().Be("Absolute Episode 11");
        metadata.SeasonNumber.Should().Be(1);
        metadata.EpisodeNumber.Should().Be(11);
        handler.RequestedPaths.Should().Contain(p => p.Contains("/episodes/default", StringComparison.Ordinal));
        handler.RequestedPaths.Should().Contain(p => p.Contains("/episodes/absolute", StringComparison.Ordinal));
    }

    [Test]
    public async Task FetchEpisodeMetadataAsync_ShouldThrow_WhenMissingFromDefaultAndAbsolute()
    {
        var handler = new TvdbStubHttpMessageHandler(includeAbsoluteEpisode: false);
        using var httpClient = new HttpClient(handler);

        var rateLimiter = new OutboundRateLimiter();
        var cooldownStore = new MetadataProviderCooldownStore();
        var auth = new TvdbAuthenticationService(
            httpClient,
            rateLimiter,
            cooldownStore,
            Substitute.For<ILogger<TvdbAuthenticationService>>());
        var apiClient = new TvdbApiClient(
            httpClient,
            auth,
            rateLimiter,
            cooldownStore,
            Substitute.For<ILogger<TvdbApiClient>>());
        var provider = new TvdbSerieMetadataProvider(
            apiClient,
            Substitute.For<ILogger<TvdbSerieMetadataProvider>>());

        var act = () => provider.FetchEpisodeMetadataAsync(
            "337018",
            seasonNumber: 1,
            episodeNumber: 11,
            language: "en",
            cancellationToken: CancellationToken.None,
            fallbackLanguage: "en");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*S1E11*337018*");
    }

    private sealed class TvdbStubHttpMessageHandler(bool includeAbsoluteEpisode = true) : HttpMessageHandler
    {
        public List<string> RequestedPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;
            RequestedPaths.Add(path);

            if (request.Method == HttpMethod.Post && path.Contains("/login", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse("""{"status":"success","data":{"token":"test-token"}}"""));
            }

            if (path.Contains("/series/337018/extended", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse("""{"status":"success","data":{"id":337018,"originalLanguage":"jpn","name":"Fate/EXTRA Last Encore"}}"""));
            }

            if (path.Contains("/series/337018/episodes/default", StringComparison.Ordinal))
            {
                if (path.Contains("page=0", StringComparison.Ordinal))
                {
                    return Task.FromResult(JsonResponse(
                        """
                        {
                          "status": "success",
                          "data": {
                            "episodes": [
                              {
                                "id": 1,
                                "seriesId": 337018,
                                "seasonNumber": 1,
                                "number": 1,
                                "absoluteNumber": 1,
                                "name": "Episode 1",
                                "aired": "2018-01-28"
                              },
                              {
                                "id": 10,
                                "seriesId": 337018,
                                "seasonNumber": 1,
                                "number": 10,
                                "absoluteNumber": 10,
                                "name": "Episode 10",
                                "aired": "2018-04-01"
                              }
                            ]
                          }
                        }
                        """));
                }

                return Task.FromResult(JsonResponse("""{"status":"success","data":{"episodes":[]}}"""));
            }

            if (path.Contains("/series/337018/episodes/absolute", StringComparison.Ordinal))
            {
                if (!includeAbsoluteEpisode)
                    return Task.FromResult(JsonResponse("""{"status":"success","data":{"episodes":[]}}"""));

                if (path.Contains("page=0", StringComparison.Ordinal))
                {
                    return Task.FromResult(JsonResponse(
                        """
                        {
                          "status": "success",
                          "data": {
                            "episodes": [
                              {
                                "id": 11,
                                "seriesId": 337018,
                                "seasonNumber": 1,
                                "number": 11,
                                "absoluteNumber": 11,
                                "name": "Absolute Episode 11",
                                "overview": "From absolute order",
                                "aired": "2018-07-29",
                                "runtime": 24
                              }
                            ]
                          }
                        }
                        """));
                }

                return Task.FromResult(JsonResponse("""{"status":"success","data":{"episodes":[]}}"""));
            }

            if (path.Contains("/episodes/11/extended", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(
                    """
                    {
                      "status": "success",
                      "data": {
                        "id": 11,
                        "seriesId": 337018,
                        "seasonNumber": 1,
                        "number": 11,
                        "absoluteNumber": 11,
                        "name": "Absolute Episode 11",
                        "overview": "From absolute order",
                        "aired": "2018-07-29",
                        "runtime": 24
                      }
                    }
                    """));
            }

            if (path.Contains("/translations/", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"Unhandled path: {path}", Encoding.UTF8, "text/plain")
            });
        }

        private static HttpResponseMessage JsonResponse(string json) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }
}

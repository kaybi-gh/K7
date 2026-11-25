using System.Net;
using System.Text;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.ExternalServices;
using K7.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Infrastructure.ExternalServices;

[TestFixture]
public class AudioMuseMusicIntelligenceAdapterTests
{
    private const string TrackId = "019fc9b4-e2c8-7344-a1fc-b33d7b371e63";

    [Test]
    public async Task GetSimilarTracksAsync_ShouldKeepSending_WhenPreviousRequestAlreadyStarted()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var adapter = CreateAdapter(http, "http://audiomuse.local:8000", "secret");
        var trackId = Guid.Parse(TrackId);

        var parallel = await Task.WhenAll(
            adapter.GetSimilarTracksAsync(trackId, 4, "Song", "Artist", CancellationToken.None),
            adapter.GetSimilarTracksAsync(trackId, 4, "Song", "Artist", CancellationToken.None));
        var again = await adapter.GetSimilarTracksAsync(trackId, 4, "Song", "Artist", CancellationToken.None);

        parallel.Should().AllSatisfy(matches => matches.Should().ContainSingle(match => match.ItemId == trackId));
        again.Should().ContainSingle(match => match.ItemId == trackId);
        handler.Calls.Should().HaveCount(3);
        handler.Calls.Should().OnlyContain(call =>
            call.Uri != null
            && call.Uri.AbsoluteUri.StartsWith("http://audiomuse.local:8000/api/similar_tracks?", StringComparison.Ordinal)
            && call.ApiKey == "secret");
        http.BaseAddress.Should().BeNull();
    }

    [Test]
    public async Task TestConnectionAsync_ShouldUseDraftUrl_AfterClientHasAlreadySent()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var adapter = CreateAdapter(http, "http://audiomuse.local:8000", "secret");

        await adapter.GetSimilarTracksAsync(Guid.Parse(TrackId), 4, "Song", "Artist", CancellationToken.None);
        var result = await adapter.TestConnectionAsync(new MusicIntelligenceSettingsDto
        {
            Enabled = false,
            BaseUrl = "http://draft.local:9000/",
            ApiKey = "draft-key"
        });

        result.Success.Should().BeTrue();
        result.Version.Should().Be("1.2.3");
        handler.Calls.Should().Contain(call =>
            call.Uri != null
            && call.Uri.AbsoluteUri == "http://draft.local:9000/api/health"
            && call.ApiKey == "draft-key");
        http.BaseAddress.Should().BeNull();
    }

    private static AudioMuseMusicIntelligenceAdapter CreateAdapter(HttpClient http, string baseUrl, string apiKey)
    {
        var json = JsonSerializer.Serialize(new MusicIntelligenceSettingsDto
        {
            Enabled = true,
            BaseUrl = baseUrl,
            ApiKey = apiKey
        });

        return new AudioMuseMusicIntelligenceAdapter(
            http,
            new StubServerSettings(json),
            Substitute.For<ILogger<AudioMuseMusicIntelligenceAdapter>>());
    }

    private sealed record RecordedCall(Uri? Uri, string? ApiKey);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly object _gate = new();

        public List<RecordedCall> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? apiKey = null;
            if (request.Headers.TryGetValues("X-Api-Key", out var values))
                apiKey = values.Single();

            lock (_gate)
                Calls.Add(new RecordedCall(request.RequestUri, apiKey));

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body = path.EndsWith("/api/health", StringComparison.Ordinal)
                ? """{"version":"1.2.3"}"""
                : $$"""[{"item_id":"{{TrackId}}","distance":0.12}]""";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubServerSettings(string json) : IServerSettingsService
    {
        public Task<T?> GetAsync<T>(SettingKey<T> key, CancellationToken cancellationToken = default)
        {
            if (key.Name == ServerSettingKeys.AudioMuseAi.Name && json is T typed)
                return Task.FromResult<T?>(typed);

            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(SettingKey<T> key, T value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<object?> GetAsync(ISettingKey key, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(json);

        public Task SetAsync(ISettingKey key, object value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveAsync<T>(SettingKey<T> key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

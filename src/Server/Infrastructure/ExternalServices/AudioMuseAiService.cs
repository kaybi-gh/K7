using System.Net.Http.Json;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.ExternalServices;

public class AudioMuseAiService : IAudioMuseAiService
{
    private readonly HttpClient _httpClient;
    private readonly IServerSettingsService _serverSettingsService;
    private readonly ILogger<AudioMuseAiService> _logger;

    public AudioMuseAiService(HttpClient httpClient, IServerSettingsService serverSettingsService, ILogger<AudioMuseAiService> logger)
    {
        _httpClient = httpClient;
        _serverSettingsService = serverSettingsService;
        _logger = logger;
    }

    public async Task<AudioMuseAiConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await ConfigureClient(cancellationToken);
            var response = await _httpClient.GetAsync("api/health", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new AudioMuseAiConnectionResult(false, Error: $"HTTP {(int)response.StatusCode}");

            var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var version = content.TryGetProperty("version", out var v) ? v.GetString() : null;

            return new AudioMuseAiConnectionResult(true, version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AudioMuse-AI connection test failed");
            return new AudioMuseAiConnectionResult(false, Error: ex.Message);
        }
    }

    public async Task<List<Guid>> GetSimilarTracksAsync(Guid trackId, int count = 20, CancellationToken cancellationToken = default)
    {
        await ConfigureClient(cancellationToken);
        var response = await _httpClient.GetAsync($"api/tracks/{trackId}/similar?count={count}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Guid>>(cancellationToken) ?? [];
    }

    public async Task<List<Guid>> GetSonicPathAsync(Guid fromId, Guid toId, CancellationToken cancellationToken = default)
    {
        await ConfigureClient(cancellationToken);
        var response = await _httpClient.GetAsync($"api/tracks/sonic-path?from={fromId}&to={toId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Guid>>(cancellationToken) ?? [];
    }

    public async Task<List<Guid>> GetSuggestionsAsync(IEnumerable<Guid> recentTrackIds, int count = 20, CancellationToken cancellationToken = default)
    {
        await ConfigureClient(cancellationToken);
        var request = new { RecentTrackIds = recentTrackIds, Count = count };
        var response = await _httpClient.PostAsJsonAsync("api/tracks/suggestions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Guid>>(cancellationToken) ?? [];
    }

    public async Task<List<Guid>> CreateSmartPlaylistAsync(string prompt, int count = 30, CancellationToken cancellationToken = default)
    {
        await ConfigureClient(cancellationToken);
        var request = new { Prompt = prompt, Count = count };
        var response = await _httpClient.PostAsJsonAsync("api/playlists/smart", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Guid>>(cancellationToken) ?? [];
    }

    private async Task ConfigureClient(CancellationToken cancellationToken)
    {
        var json = await _serverSettingsService.GetAsync(ServerSettingKeys.AudioMuseAi, cancellationToken);

        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException("AudioMuse-AI is not configured.");

        var settings = JsonSerializer.Deserialize<AudioMuseAiSettingsDto>(json)
            ?? throw new InvalidOperationException("AudioMuse-AI settings are invalid.");

        if (!settings.Enabled)
            throw new InvalidOperationException("AudioMuse-AI is disabled.");

        _httpClient.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
        }
    }
}

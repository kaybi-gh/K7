using System.Text.Json;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Services;

public static class ScrobblerAccountMapper
{
    public static UserScrobblerAccountDto ToDto(this UserScrobblerAccount account, string? unprotectedConfigJson = null)
    {
        string? presetId = null;
        string? url = null;
        string? method = null;
        string? playTemplate = null;
        string? pauseTemplate = null;
        string? stopTemplate = null;
        string? progressTemplate = null;
        string? scrobbleTemplate = null;
        IReadOnlyList<string> webhookEvents = [];

        var json = unprotectedConfigJson;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("presetId", out var preset))
                    presetId = preset.GetString();
                if (root.TryGetProperty("url", out var urlProp))
                    url = urlProp.GetString();
                if (root.TryGetProperty("method", out var methodProp))
                    method = methodProp.GetString();
                if (root.TryGetProperty("playTemplate", out var play))
                    playTemplate = play.GetString();
                if (root.TryGetProperty("pauseTemplate", out var pause))
                    pauseTemplate = pause.GetString();
                if (root.TryGetProperty("stopTemplate", out var stop))
                    stopTemplate = stop.GetString();
                if (root.TryGetProperty("progressTemplate", out var progress))
                    progressTemplate = progress.GetString();
                if (root.TryGetProperty("scrobbleTemplate", out var scrobble))
                    scrobbleTemplate = scrobble.GetString();
                if (root.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
                {
                    webhookEvents = events.EnumerateArray()
                        .Select(e => e.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s!)
                        .ToList();
                }
            }
            catch (JsonException)
            {
            }
        }

        return new UserScrobblerAccountDto
        {
            Id = account.Id,
            Provider = account.Provider.ToString(),
            IsEnabled = account.IsEnabled,
            MediaTypes = account.MediaTypes.Select(t => t.ToString()).ToList(),
            IncludeNowPlaying = account.IncludeNowPlaying,
            DisplayName = account.DisplayName,
            PresetId = presetId,
            Url = url,
            Method = method,
            WebhookEvents = webhookEvents,
            PlayTemplate = playTemplate,
            PauseTemplate = pauseTemplate,
            StopTemplate = stopTemplate,
            ProgressTemplate = progressTemplate,
            ScrobbleTemplate = scrobbleTemplate,
            Created = account.Created
        };
    }

    private static readonly MediaType[] MusicOnly = [MediaType.MusicTrack];
    private static readonly MediaType[] VideoOnly = [MediaType.Movie, MediaType.SerieEpisode];
    private static readonly MediaType[] AllTypes = [MediaType.Movie, MediaType.SerieEpisode, MediaType.MusicTrack];

    public static IReadOnlyList<MediaType> AllowedMediaTypes(
        ScrobblerProvider provider,
        string? webhookPresetId = null) => provider switch
    {
        ScrobblerProvider.LastFm or ScrobblerProvider.ListenBrainz => MusicOnly,
        ScrobblerProvider.Trakt => VideoOnly,
        ScrobblerProvider.Webhook => AllowedWebhookMediaTypes(webhookPresetId),
        _ => AllTypes
    };

    public static List<MediaType> NormalizeMediaTypes(
        ScrobblerProvider provider,
        IReadOnlyList<string>? values,
        string? webhookPresetId = null)
    {
        var allowed = AllowedMediaTypes(provider, webhookPresetId);
        if (values is null || values.Count == 0)
            return allowed.ToList();

        var selected = new List<MediaType>();
        foreach (var value in values)
        {
            if (!Enum.TryParse<MediaType>(value, out var type))
                continue;
            if (allowed.Contains(type) && !selected.Contains(type))
                selected.Add(type);
        }

        return selected.Count == 0 ? allowed.ToList() : selected;
    }

    public static List<MediaType> ParseMediaTypes(IReadOnlyList<string> values) =>
        NormalizeMediaTypes(ScrobblerProvider.Webhook, values);

    public static string? ReadWebhookPresetId(string? unprotectedConfigJson)
    {
        if (string.IsNullOrWhiteSpace(unprotectedConfigJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(unprotectedConfigJson);
            if (doc.RootElement.TryGetProperty("presetId", out var preset))
                return preset.GetString();
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static IReadOnlyList<MediaType> AllowedWebhookMediaTypes(string? presetId)
    {
        var preset = ScrobbleWebhookPresets.Find(presetId);
        if (preset is null || preset.MediaTypes.Count == 0)
            return AllTypes;

        var types = new List<MediaType>();
        foreach (var name in preset.MediaTypes)
        {
            if (Enum.TryParse<MediaType>(name, out var type) && !types.Contains(type))
                types.Add(type);
        }

        return types.Count == 0 ? AllTypes : types;
    }
}

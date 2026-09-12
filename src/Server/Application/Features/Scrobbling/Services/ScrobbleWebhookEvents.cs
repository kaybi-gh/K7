using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Scrobbling.Services;

public static class ScrobbleWebhookEvents
{
    public const string Play = "play";
    public const string Pause = "pause";
    public const string Stop = "stop";
    public const string Progress = "progress";
    public const string Scrobble = "scrobble";

    public static readonly IReadOnlyList<string> All = [Play, Pause, Stop, Progress, Scrobble];

    public static List<string> ForStorage(IEnumerable<string>? values)
    {
        var selected = Parse(values);
        return selected.Count == 0 ? All.ToList() : selected;
    }

    public static List<string> Parse(IEnumerable<string>? values)
    {
        var selected = new List<string>();
        if (values is null)
            return selected;

        foreach (var value in values)
        {
            var key = value.Trim().ToLowerInvariant();
            if (All.Any(known => known.Equals(key, StringComparison.OrdinalIgnoreCase))
                && !selected.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                selected.Add(key);
            }
        }

        return selected;
    }

    public static bool IsEnabled(IEnumerable<string>? events, string? eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            return false;

        // Legacy configs without events keep all events enabled.
        if (events is null)
            return true;

        var selected = Parse(events);
        if (selected.Count == 0)
            return true;

        return selected.Contains(eventKey, StringComparer.OrdinalIgnoreCase);
    }

    public static string? ForPlaybackState(PlaybackState state, bool isCompleted, bool isProgressTick = false)
    {
        if (isCompleted)
            return Scrobble;

        if (isProgressTick)
            return Progress;

        return state switch
        {
            PlaybackState.Playing => Play,
            PlaybackState.Paused => Pause,
            PlaybackState.Ended or PlaybackState.Idle => Stop,
            // Buffering is not Progress. Progress comes from throttled position ticks while playing.
            _ => null
        };
    }
}

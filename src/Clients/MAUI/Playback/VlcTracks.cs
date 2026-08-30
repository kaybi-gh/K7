#if WINDOWS
using System.Globalization;
using System.Text;
using LibVLCSharp;

namespace K7.Clients.MAUI.Playback;

internal static class VlcTracks
{
    public static MediaTrack[] Snapshot(MediaPlayer player, TrackType type)
    {
        // LibVLC can throw libvlc_media_track_retain while the ES list is still
        // mutating (common on Android TV right after first frame).
        try
        {
            using var list = player.Tracks(type);
            if (list is null || list.Count == 0)
                return [];

            var tracks = new List<MediaTrack>((int)list.Count);
            for (var i = 0u; i < list.Count; i++)
            {
                try
                {
                    var track = list[i];
                    if (track is not null)
                        tracks.Add(track);
                }
                catch
                {
                }
            }

            return tracks.ToArray();
        }
        catch
        {
            return [];
        }
    }

    public static void DisposeAll(MediaTrack[] tracks)
    {
        foreach (var track in tracks)
        {
            try
            {
                track.Dispose();
            }
            catch
            {
            }
        }
    }

    public static string? SelectedId(MediaPlayer player, TrackType type)
    {
        try
        {
            using var selected = player.SelectedTrack(type);
            return selected?.Id;
        }
        catch
        {
            return null;
        }
    }

    public static bool TryResolve(
        MediaTrack[] tracks,
        int? ordinal,
        string? language,
        string? name,
        out int index,
        out MediaTrack track)
    {
        if (!string.IsNullOrEmpty(name))
        {
            for (var i = 0; i < tracks.Length; i++)
            {
                if (TrackLabel(tracks[i]).Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    track = tracks[i];
                    return true;
                }
            }
        }

        if (ordinal is int o && o >= 0 && o < tracks.Length)
        {
            index = o;
            track = tracks[o];
            return true;
        }

        if (!string.IsNullOrEmpty(language))
        {
            for (var i = 0; i < tracks.Length; i++)
            {
                if (LanguageMatches(tracks[i], language))
                {
                    index = i;
                    track = tracks[i];
                    return true;
                }
            }
        }

        index = -1;
        track = null!;
        return false;
    }

    public static void Log(
        string kind,
        MediaTrack[] tracks,
        int? wantOrdinal,
        string? language,
        string? name,
        string? selectedId)
    {
        var parts = new StringBuilder();
        parts.Append("vlc es ");
        parts.Append(kind);
        parts.Append(" n=");
        parts.Append(tracks.Length);
        parts.Append(" wantOrd=");
        parts.Append(wantOrdinal?.ToString(CultureInfo.InvariantCulture) ?? "-");
        if (!string.IsNullOrEmpty(name))
        {
            parts.Append(" wantName=");
            parts.Append(name);
        }

        if (!string.IsNullOrEmpty(language))
        {
            parts.Append(" lang=");
            parts.Append(language);
        }

        parts.Append(" sel=");
        parts.Append(selectedId ?? "-");
        for (var i = 0; i < tracks.Length; i++)
        {
            parts.Append(" [");
            parts.Append(i);
            parts.Append("] id=");
            parts.Append(tracks[i].Id ?? "-");
            parts.Append(" '");
            parts.Append(TrackLabel(tracks[i]));
            parts.Append('\'');
        }

        VlcPlayerLog.Info(parts.ToString());
    }

    private static string TrackLabel(MediaTrack track) =>
        track.Name
        ?? track.Description
        ?? track.Language
        ?? "-";

    private static bool LanguageMatches(MediaTrack track, string language)
    {
        var tag = language.Trim();
        if (tag.Length == 0)
            return false;

        if (track.Language is { Length: > 0 } lang
            && (lang.Equals(tag, StringComparison.OrdinalIgnoreCase)
                || lang.StartsWith(tag, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return LanguageTagMatches(TrackLabel(track), tag);
    }

    private static bool LanguageTagMatches(string trackName, string tag)
    {
        var open = trackName.LastIndexOf('[');
        var close = trackName.LastIndexOf(']');
        if (open >= 0 && close > open + 1)
        {
            var inside = trackName[(open + 1)..close].Trim();
            if (inside.Equals(tag, StringComparison.OrdinalIgnoreCase)
                || inside.StartsWith(tag + " ", StringComparison.OrdinalIgnoreCase)
                || (tag.Length <= 3 && inside.StartsWith(tag, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (tag.Equals("en", StringComparison.OrdinalIgnoreCase)
                && inside.Equals("English", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (tag.Equals("fr", StringComparison.OrdinalIgnoreCase)
                && inside.Equals("French", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return tag.Length > 3 && trackName.Contains(tag, StringComparison.OrdinalIgnoreCase);
    }
}
#endif

using K7.Shared.Dtos.Scrobbling;
using K7.Shared.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Services;

public static class ScrobbleWebhookPresets
{
    private static readonly IReadOnlyList<string> VideoOnly = ["Movie", "SerieEpisode"];
    private static readonly IReadOnlyList<string> PlayAndScrobble = ["play", "scrobble"];
    private static readonly IReadOnlyList<string> ScrobbleOnly = ["scrobble"];

    public static IReadOnlyList<ScrobbleWebhookPresetDto> All { get; } =
    [
        new()
        {
            Id = "yamtrack",
            DisplayNameKey = "PresetYamtrack",
            UrlHint = "https://yamtrack.yourdomain.tld/webhook/jellyfin/{token}",
            PlayTemplate = Jellyfin("Play", played: false),
            PauseTemplate = Jellyfin("Pause", played: false),
            StopTemplate = Jellyfin("Stop", played: false),
            // Yamtrack only accepts Play/Stop. Progress must not reuse Play or it reopens In Progress after Completed.
            ProgressTemplate = Jellyfin("Progress", played: false),
            ScrobbleTemplate = Jellyfin("Stop", played: true),
            MediaTypes = VideoOnly,
            DefaultEvents = PlayAndScrobble
        },
        new()
        {
            Id = "floppy",
            DisplayNameKey = "PresetFloppy",
            UrlHint = "https://floppy.yourdomain.tld/webhook/jellyfin/{token}",
            PlayTemplate = Jellyfin("Play", played: false),
            PauseTemplate = Jellyfin("Pause", played: false),
            StopTemplate = Jellyfin("Stop", played: false),
            ProgressTemplate = Jellyfin("Progress", played: false),
            ScrobbleTemplate = Jellyfin("Stop", played: true),
            MediaTypes = VideoOnly,
            DefaultEvents = PlayAndScrobble
        },
        new()
        {
            Id = "ryot",
            DisplayNameKey = "PresetRyot",
            UrlHint = "https://ryot.yourdomain.tld/_i/{slug}",
            // Ryot Jellyfin sink expects unofficial plugin Default payload (not a custom action JSON).
            PlayTemplate = Jellyfin("Play", played: false),
            PauseTemplate = Jellyfin("Pause", played: false),
            StopTemplate = Jellyfin("Stop", played: false),
            ProgressTemplate = Jellyfin("Progress", played: false),
            ScrobbleTemplate = Jellyfin("Stop", played: true),
            MediaTypes = VideoOnly
        },
        new()
        {
            Id = "betaseries",
            DisplayNameKey = "PresetBetaSeries",
            UrlHint = "https://www.betaseries.com/plex/webhook?token=",
            SetupHelpUrl = ScrobblerSetupUrls.BetaSeriesApi,
            // BetaSeries only marks watched on media.scrobble (no in-progress). Movies need IMDb, episodes need TVDB.
            PlayTemplate = Plex("media.play"),
            PauseTemplate = Plex("media.pause"),
            StopTemplate = Plex("media.stop"),
            ProgressTemplate = Plex("media.play"),
            ScrobbleTemplate = Plex("media.scrobble"),
            MediaTypes = VideoOnly,
            DefaultEvents = ScrobbleOnly
        },
        new()
        {
            Id = "custom",
            DisplayNameKey = "PresetCustom",
            UrlHint = "https://example.com/webhook",
            PlayTemplate = Custom("play"),
            PauseTemplate = Custom("pause"),
            StopTemplate = Custom("stop"),
            ProgressTemplate = Custom("progress"),
            ScrobbleTemplate = Custom("scrobble")
        }
    ];

    public static ScrobbleWebhookPresetDto? Find(string? presetId) =>
        string.IsNullOrWhiteSpace(presetId)
            ? null
            : All.FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Unofficial Jellyfin webhook shape used by Yamtrack, Floppy, and Ryot:
    /// Event, Item.Type Movie|Episode, ProviderIds, Session.PlayState.PositionTicks, UserData.Played.
    /// Numeric fields use {{{ }}} so they stay JSON numbers.
    /// </summary>
    private static string Jellyfin(string jellyfinEvent, bool played)
    {
        var playedLiteral = played ? "true" : "false";
        return """
        {
          "Event": "__EVENT__",
          "User": { "Name": "{{UserName}}" },
          "Item": {
            "Name": "{{Title}}",
            "Type": "{{MediaType|SerieEpisode=Episode|Movie=Movie|*=Movie}}",
            "ProductionYear": {{{Year|empty=null}}},
            "ProviderIds": {
              "Tmdb": "{{Tmdb}}",
              "Imdb": "{{Imdb}}",
              "Tvdb": "{{Tvdb}}"
            },
            "SeriesName": "{{ShowName}}",
            "ParentIndexNumber": {{{SeasonNumber|empty=null}}},
            "IndexNumber": {{{EpisodeNumber|empty=null}}},
            "RunTimeTicks": {{{DurationTicks}}},
            "PlaybackPositionTicks": {{{PositionTicks}}},
            "UserData": {
              "Played": __PLAYED__
            }
          },
          "Series": {
            "ProviderIds": {
              "Tmdb": "{{ShowTmdb}}",
              "Tvdb": "{{ShowTvdb}}"
            }
          },
          "Session": {
            "PlayState": {
              "PositionTicks": {{{PositionTicks}}}
            }
          },
          "PlaybackPositionTicks": {{{PositionTicks}}}
        }
        """
            .Replace("__EVENT__", jellyfinEvent, StringComparison.Ordinal)
            .Replace("__PLAYED__", playedLiteral, StringComparison.Ordinal);
    }

    /// <summary>
    /// Plex-shaped body for BetaSeries. Guid fields are filled at send time via {{{PlexGuidJson}}} /
    /// {{PlexLegacyGuid}} so empty imdb:// / tvdb:// entries are never emitted.
    /// </summary>
    private static string Plex(string eventName) =>
        """
        {
          "payload": {
            "event": "__EVENT__",
            "user": true,
            "owner": true,
            "Account": { "id": 1, "thumb": "", "title": "{{UserName}}" },
            "Metadata": {
              "librarySectionType": "{{MediaType|SerieEpisode=show|Movie=movie|*=movie}}",
              "type": "{{MediaType|SerieEpisode=episode|Movie=movie|*=movie}}",
              "title": "{{Title}}",
              "year": {{{Year|empty=null}}},
              "grandparentTitle": "{{ShowName}}",
              "parentTitle": "Season {{SeasonNumber}}",
              "parentIndex": {{{SeasonNumber|empty=null}}},
              "index": {{{EpisodeNumber|empty=null}}},
              "guid": "{{PlexLegacyGuid}}",
              "Guid": {{{PlexGuidJson}}}
            }
          }
        }
        """.Replace("__EVENT__", eventName, StringComparison.Ordinal);

    private static string Custom(string eventName) =>
        """
        {
          "event": "__EVENT__",
          "title": "{{Title}}",
          "progress": {{{ProgressPercent}}},
          "state": "{{State}}",
          "mediaType": "{{MediaType}}",
          "tmdb": "{{Tmdb}}",
          "imdb": "{{Imdb}}",
          "tvdb": "{{Tvdb}}"
        }
        """.Replace("__EVENT__", eventName, StringComparison.Ordinal);
}

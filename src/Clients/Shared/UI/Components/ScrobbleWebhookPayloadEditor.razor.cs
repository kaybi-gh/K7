using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using K7.Shared.Dtos.Notifications;

namespace K7.Clients.Shared.UI.Components;

public partial class ScrobbleWebhookPayloadEditor
{
    private static readonly string[] _allEvents = ["play", "pause", "stop", "progress", "scrobble"];

    private static readonly IReadOnlyList<ButtonGroupOption<bool>> _previewModeOptions =
    [
        new(false, Icon: Phosphor.PencilSimple),
        new(true, Icon: Phosphor.Eye)
    ];

    private static readonly IReadOnlyList<NotificationParameterInfoDto> _parameters =
    [
        P("UserId", "User id", "User", "Guid", "a1b2c3d4-..."),
        P("UserName", "User name", "User", "Text", "alice"),
        P("MediaId", "Media id", "Media", "Guid", "e5f6..."),
        P("Title", "Title", "Media", "Text", "Inception"),
        P("OriginalTitle", "Original title", "Media", "Text", "Inception"),
        P("MediaType", "Media type", "Media", "Text", "Movie"),
        P("Year", "Year", "Media", "Number", "2010"),
        P("Artist", "Artist", "Music", "Text", "Radiohead"),
        P("Album", "Album", "Music", "Text", "OK Computer"),
        P("TrackName", "Track name", "Music", "Text", "Karma Police"),
        P("TrackNumber", "Track number", "Music", "Number", "6"),
        P("ShowName", "Show name", "TV", "Text", "Breaking Bad"),
        P("SeasonNumber", "Season number", "TV", "Number", "1"),
        P("EpisodeNumber", "Episode number", "TV", "Number", "1"),
        P("EpisodeName", "Episode name", "TV", "Text", "Pilot"),
        P("Tmdb", "TMDB id", "Ids", "Text", "550"),
        P("Imdb", "IMDB id", "Ids", "Text", "tt0137523"),
        P("Tvdb", "TVDB id", "Ids", "Text", "12345"),
        P("MusicBrainz", "MusicBrainz id", "Ids", "Text", "mbid"),
        P("ProgressPercent", "Progress %", "Playback", "Number", "42.5"),
        P("PositionSeconds", "Position (s)", "Playback", "Number", "120"),
        P("DurationSeconds", "Duration (s)", "Playback", "Number", "240"),
        P("PositionTicks", "Position ticks", "Playback", "Number", "1200000000"),
        P("DurationTicks", "Duration ticks", "Playback", "Number", "2400000000"),
        P("State", "Playback state", "Playback", "Text", "Playing"),
        P("IsCompleted", "Completed", "Playback", "Boolean", "false"),
        P("Timestamp", "Timestamp (ISO)", "Playback", "Text", "2026-01-01T12:00:00Z")
    ];

    [Parameter] public HashSet<string> EnabledEvents { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [Parameter] public EventCallback<HashSet<string>> EnabledEventsChanged { get; set; }
    [Parameter] public string PlayTemplate { get; set; } = "";
    [Parameter] public EventCallback<string> PlayTemplateChanged { get; set; }
    [Parameter] public string PauseTemplate { get; set; } = "";
    [Parameter] public EventCallback<string> PauseTemplateChanged { get; set; }
    [Parameter] public string StopTemplate { get; set; } = "";
    [Parameter] public EventCallback<string> StopTemplateChanged { get; set; }
    [Parameter] public string ProgressTemplate { get; set; } = "";
    [Parameter] public EventCallback<string> ProgressTemplateChanged { get; set; }
    [Parameter] public string ScrobbleTemplate { get; set; } = "";
    [Parameter] public EventCallback<string> ScrobbleTemplateChanged { get; set; }
    /// <summary>
    /// When true, templates and events are display-only (named webhook presets).
    /// </summary>
    [Parameter] public bool ReadOnly { get; set; }

    private K7TextField<string>? _templateField;
    private string _activeTab = "play";
    private bool _isPreviewMode;
    private List<TabOption<string>> _tabOptions = [];

    private IReadOnlyList<K7GroupedListGroup<NotificationParameterInfoDto>> ParameterGroups =>
        _parameters
            .GroupBy(p => p.Group)
            .OrderBy(g => GroupOrder(g.Key))
            .Select(g => new K7GroupedListGroup<NotificationParameterInfoDto>
            {
                Key = g.Key,
                Label = LocalizeGroup(g.Key),
                Items = g.ToList()
            })
            .ToList();

    protected override void OnParametersSet()
    {
        _tabOptions = EnabledEvents
            .OrderBy(e => Array.IndexOf(_allEvents, e.ToLowerInvariant()))
            .Select(e => new TabOption<string>(e, GetEventLabel(e)))
            .ToList();

        if (_tabOptions.Count == 0)
            return;

        if (!_tabOptions.Any(t => string.Equals(t.Value, _activeTab, StringComparison.OrdinalIgnoreCase)))
            _activeTab = _tabOptions[0].Value!;
    }

    private string GetEventLabel(string eventKey) => eventKey.ToLowerInvariant() switch
    {
        "play" => L["EventPlay"],
        "pause" => L["EventPause"],
        "stop" => L["EventStop"],
        "progress" => L["EventProgress"],
        "scrobble" => L["EventScrobble"],
        _ => eventKey
    };

    private string LocalizeGroup(string group) => group switch
    {
        "User" => L["GroupUser"],
        "Media" => L["GroupMedia"],
        "Music" => L["GroupMusic"],
        "TV" => L["GroupTv"],
        "Ids" => L["GroupIds"],
        "Playback" => L["GroupPlayback"],
        _ => group
    };

    private static int GroupOrder(string group) => group switch
    {
        "User" => 0,
        "Media" => 1,
        "Music" => 2,
        "TV" => 3,
        "Ids" => 4,
        "Playback" => 5,
        _ => 99
    };

    private static string FormatParamDescription(NotificationParameterInfoDto param)
    {
        var token = "{{" + param.Name + "}}";
        return string.IsNullOrWhiteSpace(param.SampleValue) ? token : $"{token}  {param.SampleValue}";
    }

    private string RenderPreview(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return "";

        var withRaw = Regex.Replace(
            template,
            @"\{\{\{(.+?)\}\}\}",
            match => ResolvePreviewToken(match.Groups[1].Value.Trim(), match.Value),
            RegexOptions.Singleline);

        return Regex.Replace(
            withRaw,
            @"\{\{([^{].*?)\}\}",
            match => ResolvePreviewToken(match.Groups[1].Value.Trim(), match.Value),
            RegexOptions.Singleline);
    }

    private string ResolvePreviewToken(string expression, string original)
    {
        var parts = expression.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return original;

        var key = parts[0];
        var param = _parameters.FirstOrDefault(p =>
            string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
        var sample = param is null || string.IsNullOrWhiteSpace(param.SampleValue)
            ? key
            : param.SampleValue;

        if (parts.Length == 1)
            return sample;

        string? fallback = null;
        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;

            var mapKey = part[..eq].Trim();
            var mapValue = part[(eq + 1)..];
            if (mapKey == "*")
            {
                fallback = mapValue;
                continue;
            }

            if (string.Equals(mapKey, sample, StringComparison.OrdinalIgnoreCase))
                return mapValue;
        }

        return fallback ?? sample;
    }

    private async Task InsertParam(string paramName)
    {
        if (ReadOnly)
            return;

        if (_isPreviewMode)
            _isPreviewMode = false;

        var token = "{{" + paramName + "}}";
        if (_templateField is not null)
        {
            await _templateField.InsertAtCursorAsync(token);
            return;
        }

        await OnActiveTemplateChanged(GetActiveTemplate() + token);
    }

    private async Task OnEventToggled(string eventKey, bool enabled)
    {
        var next = new HashSet<string>(EnabledEvents, StringComparer.OrdinalIgnoreCase);
        if (enabled)
            next.Add(eventKey);
        else
            next.Remove(eventKey);

        await EnabledEventsChanged.InvokeAsync(next);
    }

    private async Task OnTabChanged(string value)
    {
        _activeTab = value;
        await Task.CompletedTask;
    }

    private string GetActiveTemplate() => _activeTab.ToLowerInvariant() switch
    {
        "play" => PlayTemplate,
        "pause" => PauseTemplate,
        "stop" => StopTemplate,
        "progress" => ProgressTemplate,
        "scrobble" => ScrobbleTemplate,
        _ => ""
    };

    private async Task OnActiveTemplateChanged(string? value)
    {
        if (ReadOnly)
            return;

        var text = value ?? "";
        switch (_activeTab.ToLowerInvariant())
        {
            case "play":
                await PlayTemplateChanged.InvokeAsync(text);
                break;
            case "pause":
                await PauseTemplateChanged.InvokeAsync(text);
                break;
            case "stop":
                await StopTemplateChanged.InvokeAsync(text);
                break;
            case "progress":
                await ProgressTemplateChanged.InvokeAsync(text);
                break;
            case "scrobble":
                await ScrobbleTemplateChanged.InvokeAsync(text);
                break;
        }
    }

    private static NotificationParameterInfoDto P(
        string name, string displayName, string group, string valueType, string sample) =>
        new()
        {
            Name = name,
            DisplayName = displayName,
            ValueType = valueType,
            Group = group,
            SampleValue = sample
        };
}

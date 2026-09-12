using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Scrobbling;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Dialogs;

public partial class EditScrobblerAccountDialog
{
    private static readonly MediaType[] MusicOnly = [MediaType.MusicTrack];
    private static readonly MediaType[] VideoOnly = [MediaType.Movie, MediaType.SerieEpisode];
    private static readonly MediaType[] AllScrobbleTypes = [MediaType.Movie, MediaType.SerieEpisode, MediaType.MusicTrack];

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public UserScrobblerAccountDto Account { get; set; } = null!;
    [Parameter] public EventCallback OnSaved { get; set; }

    private bool _busy;
    private ScrobblerProvider _provider;
    private string _displayName = "";
    private bool _includeNowPlaying = true;
    private string _token = "";
    private string _url = "";
    private string? _setupHelpUrl;
    private string _presetId = "custom";
    private string _playTemplate = "";
    private string _pauseTemplate = "";
    private string _stopTemplate = "";
    private string _progressTemplate = "";
    private string _scrobbleTemplate = "";
    private HashSet<string> _webhookEvents = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<MediaType> _selectedMediaTypes = [];
    private IReadOnlyList<MediaType> _availableMediaTypes = AllScrobbleTypes;

    private bool ShowsMediaTypePicker =>
        _provider is ScrobblerProvider.Trakt or ScrobblerProvider.Webhook;

    private bool IsCustomWebhook =>
        _provider == ScrobblerProvider.Webhook
        && (string.IsNullOrWhiteSpace(Account.PresetId)
            || string.Equals(Account.PresetId, "custom", StringComparison.OrdinalIgnoreCase));

    private string WebhookUrlHelperText
    {
        get
        {
            var key = _presetId.ToLowerInvariant() switch
            {
                "yamtrack" => "WebhookUrlHintYamtrack",
                "floppy" => "WebhookUrlHintFloppy",
                "ryot" => "WebhookUrlHintRyot",
                "betaseries" => "WebhookUrlHintBetaSeries",
                _ => "WebhookUrlHintCustom"
            };
            return L[key];
        }
    }
    private bool CanSave
    {
        get
        {
            if (_selectedMediaTypes.Count == 0)
                return false;

            if (_provider != ScrobblerProvider.Webhook)
                return true;

            if (string.IsNullOrWhiteSpace(_url) || _webhookEvents.Count == 0)
                return false;

            if (IsCustomWebhook)
            {
                foreach (var eventKey in _webhookEvents)
                {
                    var template = eventKey.ToLowerInvariant() switch
                    {
                        "play" => _playTemplate,
                        "pause" => _pauseTemplate,
                        "stop" => _stopTemplate,
                        "progress" => _progressTemplate,
                        "scrobble" => _scrobbleTemplate,
                        _ => null
                    };
                    if (string.IsNullOrWhiteSpace(template))
                        return false;
                }
            }

            return true;
        }
    }

    private string ProviderLabel => _provider switch
    {
        ScrobblerProvider.LastFm => L["ProviderLastFm"],
        ScrobblerProvider.ListenBrainz => L["ProviderListenBrainz"],
        ScrobblerProvider.Trakt => L["ProviderTrakt"],
        ScrobblerProvider.Webhook => L["ProviderWebhook"],
        _ => Account.Provider
    };

    private string CapabilityHint => _provider switch
    {
        ScrobblerProvider.LastFm => L["CapabilityLastFm"],
        ScrobblerProvider.ListenBrainz => L["CapabilityListenBrainz"],
        ScrobblerProvider.Trakt => L["CapabilityTrakt"],
        ScrobblerProvider.Webhook when string.Equals(Account.PresetId, "betaseries", StringComparison.OrdinalIgnoreCase)
            => L["CapabilityBetaSeries"],
        ScrobblerProvider.Webhook when IsVideoOnlyWebhookPreset()
            => L["CapabilityWebhookVideo"],
        ScrobblerProvider.Webhook => L["CapabilityWebhook"],
        _ => ""
    };

    protected override async Task OnInitializedAsync()
    {
        Enum.TryParse(Account.Provider, out _provider);
        _displayName = Account.DisplayName ?? "";
        _includeNowPlaying = Account.IncludeNowPlaying;
        _url = Account.Url ?? "";
        _presetId = Account.PresetId ?? "custom";
        _playTemplate = Account.PlayTemplate ?? "";
        _pauseTemplate = Account.PauseTemplate ?? "";
        _stopTemplate = Account.StopTemplate ?? "";
        _progressTemplate = Account.ProgressTemplate ?? "";
        _scrobbleTemplate = Account.ScrobbleTemplate ?? Account.StopTemplate ?? "";
        _webhookEvents = Account.WebhookEvents.Count > 0
            ? Account.WebhookEvents.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "play", "pause", "stop", "progress", "scrobble"
            };

        _availableMediaTypes = _provider switch
        {
            ScrobblerProvider.LastFm or ScrobblerProvider.ListenBrainz => MusicOnly,
            ScrobblerProvider.Trakt => VideoOnly,
            ScrobblerProvider.Webhook when IsVideoOnlyWebhookPreset() => VideoOnly,
            _ => AllScrobbleTypes
        };

        _selectedMediaTypes = Account.MediaTypes
            .Select(name => Enum.TryParse<MediaType>(name, out var type) ? type : (MediaType?)null)
            .Where(type => type is not null && _availableMediaTypes.Contains(type.Value))
            .Select(type => type!.Value)
            .ToHashSet();

        if (_selectedMediaTypes.Count == 0)
            _selectedMediaTypes = _availableMediaTypes.ToHashSet();

        if (_provider == ScrobblerProvider.Webhook)
            await ApplyLivePresetTemplatesAsync();
    }

    private async Task ApplyLivePresetTemplatesAsync()
    {
        try
        {
            var presets = await Scrobbling.GetWebhookPresetsAsync();
            var preset = presets.FirstOrDefault(p =>
                string.Equals(p.Id, Account.PresetId, StringComparison.OrdinalIgnoreCase));
            if (preset is null)
                return;

            _setupHelpUrl = preset.SetupHelpUrl;
            if (!IsCustomWebhook)
            {
                _playTemplate = preset.PlayTemplate;
                _pauseTemplate = preset.PauseTemplate;
                _stopTemplate = preset.StopTemplate;
                _progressTemplate = preset.ProgressTemplate;
                _scrobbleTemplate = preset.ScrobbleTemplate;
            }
        }
        catch
        {
            // Keep stored templates if presets cannot be loaded.
        }
    }

    private bool IsVideoOnlyWebhookPreset()
    {
        var presetId = Account.PresetId;
        return presetId is "yamtrack" or "floppy" or "ryot" or "betaseries";
    }

    private void ToggleMediaType(MediaType type, bool selected)
    {
        if (selected)
            _selectedMediaTypes.Add(type);
        else
            _selectedMediaTypes.Remove(type);
    }

    private string GetMediaTypeLabel(MediaType type) => type switch
    {
        MediaType.Movie => L["MediaTypeMovie"],
        MediaType.SerieEpisode => L["MediaTypeEpisode"],
        MediaType.MusicTrack => L["MediaTypeMusic"],
        _ => type.ToString()
    };

    private Task OnWebhookEventsChanged(HashSet<string> events)
    {
        _webhookEvents = events;
        return Task.CompletedTask;
    }

    private void Cancel() => Dialog.Cancel();

    private async Task SaveAsync()
    {
        if (!CanSave)
            return;

        _busy = true;
        try
        {
            await Scrobbling.UpdateAccountAsync(Account.Id, new UpdateUserScrobblerAccountRequest
            {
                IsEnabled = Account.IsEnabled,
                IncludeNowPlaying = _provider == ScrobblerProvider.Webhook
                    ? _webhookEvents.Contains("play")
                    : _includeNowPlaying,
                DisplayName = string.IsNullOrWhiteSpace(_displayName) ? null : _displayName.Trim(),
                MediaTypes = _selectedMediaTypes.Select(t => t.ToString()).ToList(),
                Token = string.IsNullOrWhiteSpace(_token) ? null : _token.Trim(),
                Url = _provider == ScrobblerProvider.Webhook ? _url.Trim() : null,
                Method = _provider == ScrobblerProvider.Webhook ? (Account.Method ?? "POST") : null,
                PresetId = _provider == ScrobblerProvider.Webhook ? Account.PresetId : null,
                WebhookEvents = _provider == ScrobblerProvider.Webhook ? _webhookEvents.ToList() : null,
                PlayTemplate = _provider == ScrobblerProvider.Webhook ? _playTemplate : null,
                PauseTemplate = _provider == ScrobblerProvider.Webhook ? _pauseTemplate : null,
                StopTemplate = _provider == ScrobblerProvider.Webhook ? _stopTemplate : null,
                ProgressTemplate = _provider == ScrobblerProvider.Webhook ? _progressTemplate : null,
                ScrobbleTemplate = _provider == ScrobblerProvider.Webhook ? _scrobbleTemplate : null
            });

            await OnSaved.InvokeAsync();
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _busy = false;
        }
    }
}

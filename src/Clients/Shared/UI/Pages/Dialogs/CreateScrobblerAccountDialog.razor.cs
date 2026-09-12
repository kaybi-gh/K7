using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Scrobbling;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Dialogs;

public partial class CreateScrobblerAccountDialog
{
    private static readonly MediaType[] MusicOnly = [MediaType.MusicTrack];
    private static readonly MediaType[] VideoOnly = [MediaType.Movie, MediaType.SerieEpisode];
    private static readonly MediaType[] AllScrobbleTypes = [MediaType.Movie, MediaType.SerieEpisode, MediaType.MusicTrack];

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public EventCallback OnCreated { get; set; }

    private bool _loading = true;
    private bool _busy;
    private string _provider = nameof(ScrobblerProvider.ListenBrainz);
    private string _displayName = "";
    private string _listenBrainzToken = "";
    private string _webhookUrl = "";
    private string _webhookPresetId = "custom";
    private string _playTemplate = "";
    private string _pauseTemplate = "";
    private string _stopTemplate = "";
    private string _progressTemplate = "";
    private string _scrobbleTemplate = "";
    private HashSet<string> _webhookEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "play", "pause", "stop", "progress", "scrobble"
    };
    private ScrobblingAvailabilityDto _availability = new();
    private List<ScrobbleWebhookPresetDto> _presets = [];
    private LastFmAuthStartDto? _lastFm;
    private TraktDeviceStartDto? _trakt;
    private HashSet<MediaType> _selectedMediaTypes = [];
    private IReadOnlyList<MediaType> _availableMediaTypes = AllScrobbleTypes;

    private bool ProviderConfigured => _provider switch
    {
        nameof(ScrobblerProvider.LastFm) => _availability.LastFmConfigured,
        nameof(ScrobblerProvider.Trakt) => _availability.TraktConfigured,
        _ => true
    };

    private bool IsCustomWebhook =>
        _provider == nameof(ScrobblerProvider.Webhook)
        && string.Equals(_webhookPresetId, "custom", StringComparison.OrdinalIgnoreCase);

    private ScrobbleWebhookPresetDto? SelectedWebhookPreset =>
        _presets.FirstOrDefault(p => string.Equals(p.Id, _webhookPresetId, StringComparison.OrdinalIgnoreCase));

    private string WebhookUrlHelperText
    {
        get
        {
            var key = _webhookPresetId.ToLowerInvariant() switch
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

    private bool ShowsMediaTypePicker =>
        _provider is nameof(ScrobblerProvider.Trakt) or nameof(ScrobblerProvider.Webhook);

    private bool CanSubmit => !_loading && ProviderConfigured && _selectedMediaTypes.Count > 0 && _provider switch
    {
        nameof(ScrobblerProvider.LastFm) => true,
        nameof(ScrobblerProvider.Trakt) => true,
        nameof(ScrobblerProvider.ListenBrainz) => !string.IsNullOrWhiteSpace(_listenBrainzToken),
        nameof(ScrobblerProvider.Webhook) => !string.IsNullOrWhiteSpace(_webhookUrl)
            && (!IsCustomWebhook || (_webhookEvents.Count > 0 && HasTemplatesForEnabledEvents())),
        _ => false
    };

    private bool HasTemplatesForEnabledEvents()
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

        return true;
    }

    private string PrimaryLabel => _provider switch
    {
        nameof(ScrobblerProvider.LastFm) when _lastFm is not null => L["LastFmDone"],
        nameof(ScrobblerProvider.Trakt) when _trakt is not null => L["TraktDone"],
        nameof(ScrobblerProvider.LastFm) or nameof(ScrobblerProvider.Trakt) => L["Connect"],
        _ => L["Add"]
    };

    private string? ProviderHelperText
    {
        get
        {
            if (!_availability.LastFmConfigured && !_availability.TraktConfigured)
                return L["ContactAdminBoth"];
            if (!_availability.LastFmConfigured)
                return L["ContactAdminLastFm"];
            if (!_availability.TraktConfigured)
                return L["ContactAdminTrakt"];
            return null;
        }
    }

    private string ProviderCapabilityHint => _provider switch
    {
        nameof(ScrobblerProvider.LastFm) => L["CapabilityLastFm"],
        nameof(ScrobblerProvider.ListenBrainz) => L["CapabilityListenBrainz"],
        nameof(ScrobblerProvider.Trakt) => L["CapabilityTrakt"],
        nameof(ScrobblerProvider.Webhook) when string.Equals(_webhookPresetId, "betaseries", StringComparison.OrdinalIgnoreCase)
            => L["CapabilityBetaSeries"],
        nameof(ScrobblerProvider.Webhook) when IsVideoOnlyWebhookPreset(_webhookPresetId)
            => L["CapabilityWebhookVideo"],
        nameof(ScrobblerProvider.Webhook) => L["CapabilityWebhook"],
        _ => ""
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _availability = await Scrobbling.GetAvailabilityAsync();
            _presets = await Scrobbling.GetWebhookPresetsAsync();
            if (_presets.Count > 0)
            {
                var custom = _presets.FirstOrDefault(p => p.Id == "custom") ?? _presets[0];
                _webhookPresetId = custom.Id;
                ApplyWebhookPreset(custom);
            }

            _provider = FirstAvailableProvider();
            ResetMediaTypesForProvider();
            _displayName = DefaultDisplayNameForProvider(_provider);
        }
        catch
        {
            _availability = new ScrobblingAvailabilityDto();
            _presets = [];
            ResetMediaTypesForProvider();
            _displayName = DefaultDisplayNameForProvider(_provider);
        }

        _loading = false;
    }

    private string FirstAvailableProvider()
    {
        if (_availability.LastFmConfigured)
            return nameof(ScrobblerProvider.LastFm);
        return nameof(ScrobblerProvider.ListenBrainz);
    }

    private string DefaultDisplayNameForProvider(string provider) => provider switch
    {
        nameof(ScrobblerProvider.ListenBrainz) => L["ProviderListenBrainz"],
        nameof(ScrobblerProvider.Trakt) => L["ProviderTrakt"],
        nameof(ScrobblerProvider.Webhook) =>
            _presets.FirstOrDefault(p => p.Id == _webhookPresetId) is { } preset
                ? LocalizePreset(preset)
                : L["ProviderWebhook"],
        _ => ""
    };

    private void OnProviderChanged(string? provider)
    {
        _provider = provider ?? nameof(ScrobblerProvider.ListenBrainz);
        _lastFm = null;
        _trakt = null;
        _displayName = DefaultDisplayNameForProvider(_provider);
        ResetMediaTypesForProvider();
    }

    private void ResetMediaTypesForProvider()
    {
        _availableMediaTypes = _provider switch
        {
            nameof(ScrobblerProvider.LastFm) or nameof(ScrobblerProvider.ListenBrainz) => MusicOnly,
            nameof(ScrobblerProvider.Trakt) => VideoOnly,
            nameof(ScrobblerProvider.Webhook) => AvailableMediaTypesForWebhookPreset(_webhookPresetId),
            _ => AllScrobbleTypes
        };
        _selectedMediaTypes = _availableMediaTypes.ToHashSet();
    }

    private IReadOnlyList<MediaType> AvailableMediaTypesForWebhookPreset(string? presetId)
    {
        var preset = _presets.FirstOrDefault(p =>
            string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase));
        if (preset is null || preset.MediaTypes.Count == 0)
            return AllScrobbleTypes;

        var types = new List<MediaType>();
        foreach (var name in preset.MediaTypes)
        {
            if (Enum.TryParse<MediaType>(name, out var type) && !types.Contains(type))
                types.Add(type);
        }

        return types.Count == 0 ? AllScrobbleTypes : types;
    }

    private bool IsVideoOnlyWebhookPreset(string? presetId)
    {
        var types = AvailableMediaTypesForWebhookPreset(presetId);
        return types.Count > 0 && !types.Contains(MediaType.MusicTrack);
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

    private void OnWebhookPresetChanged(string? id)
    {
        _webhookPresetId = id ?? "custom";
        var preset = _presets.FirstOrDefault(p => p.Id == _webhookPresetId);
        if (preset is not null)
            ApplyWebhookPreset(preset);
        ResetMediaTypesForProvider();
    }

    private void ApplyWebhookPreset(ScrobbleWebhookPresetDto preset)
    {
        _webhookUrl = preset.UrlHint;
        _displayName = LocalizePreset(preset);
        _playTemplate = preset.PlayTemplate;
        _pauseTemplate = preset.PauseTemplate;
        _stopTemplate = preset.StopTemplate;
        _progressTemplate = preset.ProgressTemplate;
        _scrobbleTemplate = preset.ScrobbleTemplate;
        _webhookEvents = preset.DefaultEvents.Count > 0
            ? preset.DefaultEvents.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "play", "pause", "stop", "progress", "scrobble"
            };
    }

    private Task OnWebhookEventsChanged(HashSet<string> events)
    {
        _webhookEvents = events;
        return Task.CompletedTask;
    }

    private string LocalizePreset(ScrobbleWebhookPresetDto preset)
    {
        var localized = L[preset.DisplayNameKey];
        return localized.ResourceNotFound ? preset.Id : localized.Value;
    }

    private IReadOnlyList<string> SelectedMediaTypeNames() =>
        _selectedMediaTypes.Select(t => t.ToString()).ToList();

    private void Cancel() => Dialog.Cancel();

    private async Task PrimaryAsync()
    {
        if (!CanSubmit)
            return;

        _busy = true;
        try
        {
            switch (_provider)
            {
                case nameof(ScrobblerProvider.LastFm):
                    await ConnectLastFmAsync();
                    break;
                case nameof(ScrobblerProvider.Trakt):
                    await ConnectTraktAsync();
                    break;
                case nameof(ScrobblerProvider.ListenBrainz):
                    await AddListenBrainzAsync();
                    break;
                case nameof(ScrobblerProvider.Webhook):
                    await AddWebhookAsync();
                    break;
            }
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

    private async Task ConnectLastFmAsync()
    {
        if (_lastFm is null)
        {
            _lastFm = await Scrobbling.StartLastFmAuthAsync();
            return;
        }

        await Scrobbling.CompleteLastFmAuthAsync(_lastFm.Token, SelectedMediaTypeNames());
        await FinishAsync();
    }

    private async Task ConnectTraktAsync()
    {
        if (_trakt is null)
        {
            _trakt = await Scrobbling.StartTraktDeviceAsync();
            return;
        }

        var id = await Scrobbling.PollTraktDeviceAsync(
            _trakt.DeviceCode,
            SelectedMediaTypeNames(),
            string.IsNullOrWhiteSpace(_displayName) ? null : _displayName.Trim());
        if (id is null)
        {
            Snackbar.Add(L["TraktPending"], K7Severity.Info);
            return;
        }

        await FinishAsync();
    }

    private async Task AddListenBrainzAsync()
    {
        await Scrobbling.CreateAccountAsync(new CreateUserScrobblerAccountRequest
        {
            Provider = nameof(ScrobblerProvider.ListenBrainz),
            Token = _listenBrainzToken,
            DisplayName = string.IsNullOrWhiteSpace(_displayName) ? L["ProviderListenBrainz"] : _displayName.Trim(),
            MediaTypes = SelectedMediaTypeNames()
        });
        await FinishAsync();
    }

    private async Task AddWebhookAsync()
    {
        var preset = _presets.FirstOrDefault(p => p.Id == _webhookPresetId);
        await Scrobbling.CreateAccountAsync(new CreateUserScrobblerAccountRequest
        {
            Provider = nameof(ScrobblerProvider.Webhook),
            Url = _webhookUrl,
            PresetId = _webhookPresetId,
            DisplayName = string.IsNullOrWhiteSpace(_displayName)
                ? (preset is null ? L["ProviderWebhook"] : LocalizePreset(preset))
                : _displayName.Trim(),
            MediaTypes = SelectedMediaTypeNames(),
            IncludeNowPlaying = _webhookEvents.Contains("play"),
            WebhookEvents = _webhookEvents.ToList(),
            PlayTemplate = string.IsNullOrWhiteSpace(_playTemplate) ? preset?.PlayTemplate : _playTemplate,
            PauseTemplate = string.IsNullOrWhiteSpace(_pauseTemplate) ? preset?.PauseTemplate : _pauseTemplate,
            StopTemplate = string.IsNullOrWhiteSpace(_stopTemplate) ? preset?.StopTemplate : _stopTemplate,
            ProgressTemplate = string.IsNullOrWhiteSpace(_progressTemplate) ? preset?.ProgressTemplate : _progressTemplate,
            ScrobbleTemplate = string.IsNullOrWhiteSpace(_scrobbleTemplate) ? preset?.ScrobbleTemplate : _scrobbleTemplate
        });
        await FinishAsync();
    }

    private async Task FinishAsync()
    {
        await OnCreated.InvokeAsync();
        Dialog.Close(K7DialogResult.Ok(true));
    }
}

using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Helpers;
using K7.Shared.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsVideoPlaybackPage : IDisposable
{
    private sealed record VideoFormState(
        VideoPlayerSettingsDto Settings,
        VideoPlaybackPolicySettingsDto Policy,
        TrackSelectionPreferencesDto Preferences,
        Guid? LibraryId,
        bool AudioPassthrough,
        ExoVideoBufferSize ExoBuffer,
        HdmiAutoFrameRateMode HdmiAfr,
        DolbyVisionDecodeMode DvDecode,
        bool MpcEnabled,
        string MpcExePath,
        string MpcWebHost,
        string MpcWebPortText,
        string MpcExtraArgs,
        string MpcLibraryPathsJson);

    private const string BasePath = "/settings/video-playback";

    /// <summary>Sub-page to show. Null shows the index (plus the first sub-page on wide screens).</summary>
    [Parameter] public string? Section { get; set; }

    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;
    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IPlayerService PlayerService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;
    [Inject] private ILocalPathPicker LocalPathPicker { get; set; } = default!;

    private VideoPlayerSettingsDto? _settings;
    private VideoPlaybackPolicySettingsDto? _videoPolicy;
    private TrackSelectionPreferencesDto? _preferences;
    private List<LibraryDto> _libraries = [];
    private Guid? _selectedLibraryId;
    private bool _audioPassthrough = true;
    private ExoVideoBufferSize _exoBuffer = ExoVideoBufferSize.Auto;
    private HdmiAutoFrameRateMode _hdmiAfr = HdmiAutoFrameRateMode.Disabled;
    private HdmiAutoFrameRateMode _hdmiAfrDefault = HdmiAutoFrameRateMode.Disabled;
    private DolbyVisionDecodeMode _dvDecode = DolbyVisionDecodeMode.Native;
    private DolbyVisionDecodeMode _dvDecodeDefault = DolbyVisionDecodeMode.Native;
    private bool _showDeviceAdvanced;
    private bool _showExoBuffer;
    private bool _showHdmiAfr;
    private bool _showMpcExternal;
    private bool _mpcEnabled;
    private string _mpcExePath = "";
    private string _mpcWebHost = WindowsMpcPlaybackSettings.DefaultWebHost;
    private string _mpcWebPortText = WindowsMpcPlaybackSettings.DefaultWebPort.ToString();
    private string _mpcExtraArgs = WindowsMpcPlaybackSettings.DefaultExtraArgs;
    private Dictionary<Guid, string> _mpcLibraryLocalRoots = new();
    private bool _loading = true;
    private bool _saving;
    private bool _hasUserOverride;
    private readonly SettingsFormTracker<VideoFormState> _formTracker = new();
    private readonly SettingsAutoSave _autoSave = new();
    private SettingsSaveState _saveStatus;
    private bool _disposed;

    private IReadOnlyList<SettingsSection> Sections
    {
        get
        {
            var sections = new List<SettingsSection>
            {
                new(VideoSettingsSections.Tracks, Phosphor.Translate, Lt["SectionTitle"], L["TracksSectionDescription"]),
                new(VideoSettingsSections.Seekbar, Phosphor.FastForward, L["SeekbarSection"], L["SeekbarSectionDescription"]),
                new(VideoSettingsSections.Skip, Phosphor.SkipForward, L["IntroSkipSection"], L["IntroSkipSectionDescription"]),
                new(VideoSettingsSections.NextEpisode, Phosphor.Queue, L["NextEpisodeSection"], L["NextEpisodeSectionDescription"]),
                new(VideoSettingsSections.Trailers, Phosphor.FilmStrip, L["TrailersSection"], L["TrailersSectionDescription"]),
                new(VideoSettingsSections.Subtitles, Phosphor.ClosedCaptioning, L["SubtitleSection"], L["SubtitleSectionDescription"]),
                new(VideoSettingsSections.Resume, Phosphor.ClockCounterClockwise, L["ResumeSectionTitle"], L["ResumeSectionDescription"])
            };

            if (_showDeviceAdvanced)
            {
                sections.Add(new SettingsSection(
                    VideoSettingsSections.Advanced,
                    Phosphor.Wrench,
                    L["AdvancedSection"],
                    L["AdvancedSectionDescription"]));
            }

            return sections;
        }
    }

    private bool IsSectionOpen => Sections.Any(s => s.Key == Section);

    private string ActiveSection => IsSectionOpen ? Section! : VideoSettingsSections.Tracks;

    private string ActiveSectionTitle => Sections.First(s => s.Key == ActiveSection).Title;

    /// <summary>
    /// Edits waiting for the debounce already count as saving, so the indicator stays visible
    /// from the change to the end of the request instead of flashing.
    /// </summary>
    private SettingsSaveState CurrentSaveState =>
        _saveStatus != SettingsSaveState.Failed && IsDirty ? SettingsSaveState.Saving : _saveStatus;

    private bool IsDirty =>
        _settings is not null
        && _preferences is not null
        && _videoPolicy is not null
        && _formTracker.IsDirty(GetFormState());

    private bool HasDeviceVideoOverride =>
        _showDeviceAdvanced
        && (!_audioPassthrough
            || _exoBuffer != ExoVideoBufferSize.Auto
            || (_showHdmiAfr && _hdmiAfr != _hdmiAfrDefault)
            || (_showHdmiAfr && _dvDecode != _dvDecodeDefault)
            || (_showMpcExternal && !WindowsMpcPlaybackSettings.IsDefault(CurrentMpcOptions())));

    private bool ResetDisabled => !IsDirty && !_hasUserOverride && !HasDeviceVideoOverride;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _autoSave.Dispose();

        // Leaving inside the quiet time: send the last change instead of dropping it.
        if (!_saving && IsDirty)
            PersistAsync().FireAndForget();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _libraries = await LibraryService.GetLibrariesAsync();
            _settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            _videoPolicy = await UserPreferencesService.GetEffectiveVideoPlaybackPolicySettingsAsync();
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync();
            ApplyLocalVideoPlayerSettings(_settings);
            await LoadDeviceVideoExperienceAsync();
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }
        catch
        {
            _settings = new VideoPlayerSettingsDto();
            _videoPolicy = new VideoPlaybackPolicySettingsDto();
            _preferences = new TrackSelectionPreferencesDto();
            ApplyLocalVideoPlayerSettings(_settings);
            await LoadDeviceVideoExperienceAsync();
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }

        _loading = false;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (_loading || _saving || _disposed || _settings is null || _preferences is null || _videoPolicy is null)
            return;

        var state = GetFormState();
        if (!_formTracker.IsDirty(state))
            return;

        _autoSave.Schedule(_formTracker.Snapshot(state), () => InvokeAsync(SaveAsync));
    }

    private VideoFormState GetFormState() =>
        new(
            _settings!,
            _videoPolicy!,
            _preferences!,
            _selectedLibraryId,
            _audioPassthrough,
            _exoBuffer,
            _hdmiAfr,
            _dvDecode,
            _mpcEnabled,
            _mpcExePath,
            _mpcWebHost,
            _mpcWebPortText,
            _mpcExtraArgs,
            LibraryPathMirror.Serialize(_mpcLibraryLocalRoots));

    private async Task LoadDeviceVideoExperienceAsync()
    {
        _showDeviceAdvanced = DeviceService.GetClientType() == ClientType.Native;
        try
        {
            var os = await DeviceService.GetOperatingSystemAsync();
            _showExoBuffer = _showDeviceAdvanced && os == OperatingSystem.Android;
            _showMpcExternal = _showDeviceAdvanced && os == OperatingSystem.Windows;
        }
        catch
        {
            _showExoBuffer = false;
            _showMpcExternal = false;
        }

        if (!_showDeviceAdvanced)
            return;

        var isTelevision = false;
        string? manufacturer = null;
        string? model = null;
        try
        {
            isTelevision = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
            var details = await DeviceService.GetNativeDeviceDetailsAsync();
            manufacturer = details.RawManufacturer;
            model = details.RawModel;
        }
        catch
        {
        }

        _showHdmiAfr = _showExoBuffer
            && (isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model));
        _hdmiAfrDefault = HdmiAutoFrameRatePolicy.DefaultForDevice(
            isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model),
            manufacturer,
            model);
        _dvDecodeDefault = DolbyVisionDecodePolicy.DefaultForDevice(
            isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model),
            manufacturer,
            model);

        try
        {
            _audioPassthrough = DeviceStorage.Get(PreferenceKeys.VIDEO_AUDIO_PASSTHROUGH, true);
            _exoBuffer = ExoVideoBufferPolicy.Parse(
                DeviceStorage.Get(PreferenceKeys.VIDEO_EXO_BUFFER, ExoVideoBufferPolicy.Auto));
            _hdmiAfr = _showHdmiAfr
                ? HdmiAutoFrameRatePolicy.Resolve(
                    DeviceStorage.Get(PreferenceKeys.VIDEO_HDMI_AFR, ""),
                    isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model),
                    manufacturer,
                    model)
                : _hdmiAfrDefault;
            _dvDecode = _showHdmiAfr
                ? DolbyVisionDecodePolicy.Resolve(
                    DeviceStorage.Get(PreferenceKeys.VIDEO_DV_DECODE, ""),
                    isTelevision || AndroidExoPlaybackPolicy.IsAmlogicDevice(manufacturer, model),
                    manufacturer,
                    model)
                : _dvDecodeDefault;
            ApplyMpcOptions(WindowsMpcPlaybackSettings.Load(DeviceStorage));
        }
        catch
        {
            _audioPassthrough = true;
            _exoBuffer = ExoVideoBufferSize.Auto;
            _hdmiAfr = _hdmiAfrDefault;
            _dvDecode = _dvDecodeDefault;
            ApplyMpcOptions(WindowsMpcPlaybackSettings.Empty());
        }
    }

    private void PersistDeviceVideoExperience()
    {
        if (!_showDeviceAdvanced)
            return;

        DeviceStorage.Set(PreferenceKeys.VIDEO_AUDIO_PASSTHROUGH, _audioPassthrough);
        DeviceStorage.Set(PreferenceKeys.VIDEO_EXO_BUFFER, ExoVideoBufferPolicy.Persist(_exoBuffer));
        if (_showHdmiAfr)
        {
            DeviceStorage.Set(PreferenceKeys.VIDEO_HDMI_AFR, HdmiAutoFrameRatePolicy.Persist(_hdmiAfr));
            DeviceStorage.Set(PreferenceKeys.VIDEO_DV_DECODE, DolbyVisionDecodePolicy.Persist(_dvDecode));
        }

        if (_showMpcExternal)
            WindowsMpcPlaybackSettings.Save(DeviceStorage, CurrentMpcOptions());
    }

    private void CaptureFormState()
    {
        if (_settings is null || _preferences is null || _videoPolicy is null)
            return;

        _formTracker.Capture(GetFormState());
    }

    private void OnVideoPolicyChanged(VideoPlaybackPolicySettingsDto value)
    {
        _videoPolicy = value;
        StateHasChanged();
    }

    private async Task ApplyLocalVideoPlayerSettingsAsync(VideoPlayerSettingsDto settings)
    {
        PlayerService.ApplyVideoPlayerUxSettings(settings);
        await ApplySubtitleStyleAsync(settings);
    }

    private void ApplyLocalVideoPlayerSettings(VideoPlayerSettingsDto settings) =>
        ApplyLocalVideoPlayerSettingsAsync(settings).FireAndForget();

    private async Task ApplySubtitleStyleAsync(VideoPlayerSettingsDto settings)
    {
        var deviceType = DeviceService.CachedDeviceType ?? await DeviceService.GetDeviceTypeAsync();
        await SubtitleStyleApplicator.ApplyAsync(JSRuntime, settings, deviceType);
    }

    private async Task OnLibraryScopeChanged(Guid? libraryId)
    {
        if (libraryId == _selectedLibraryId)
            return;

        // Preferences are stored per scope: save the current scope before loading another.
        if (IsDirty)
        {
            await SaveAsync();
            if (IsDirty)
            {
                StateHasChanged();
                return;
            }
        }

        _selectedLibraryId = libraryId;

        try
        {
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync(_selectedLibraryId);
        }
        catch
        {
            _preferences = new TrackSelectionPreferencesDto();
        }

        CaptureFormState();
        await RefreshOverrideStateAsync();
    }

    private async Task SaveAsync()
    {
        if (_saving || _settings is null || _preferences is null || _videoPolicy is null)
            return;

        _autoSave.Cancel();
        _saving = true;
        _saveStatus = SettingsSaveState.Saving;
        StateHasChanged();
        try
        {
            // Snapshot what is sent: edits made while the request is in flight stay dirty
            // and trigger the next save.
            var sentSnapshot = _formTracker.Snapshot(GetFormState());
            var sentSettings = _settings;
            await PersistAsync();
            await ApplyLocalVideoPlayerSettingsAsync(sentSettings);
            _formTracker.CaptureSnapshot(sentSnapshot);
            _autoSave.Reset();
            await RefreshOverrideStateAsync();
            _saveStatus = SettingsSaveState.Saved;
        }
        catch (Exception ex)
        {
            _saveStatus = SettingsSaveState.Failed;
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
            if (!_disposed)
                StateHasChanged();
        }
    }

    private async Task PersistAsync()
    {
        if (_settings is null || _preferences is null || _videoPolicy is null)
            return;

        await Task.WhenAll(
            UserPreferencesService.UpdateUserVideoPlayerSettingsAsync(_settings),
            UserPreferencesService.UpdateUserVideoPlaybackPolicySettingsAsync(_videoPolicy),
            UserPreferencesService.UpdateUserTrackSelectionPreferencesAsync(_preferences, _selectedLibraryId));
        PersistDeviceVideoExperience();
    }

    private async Task ConfirmResetAsync()
    {
        if (_saving)
            return;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            S["ResetToDefaultsTitle"],
            S["ResetToDefaultsMessage"],
            yesText: S["ResetToDefaults"],
            cancelText: S["Cancel"]);

        if (confirmed is true)
            await ResetAsync();
    }

    private async Task ResetAsync()
    {
        if (_saving)
            return;

        _autoSave.Reset();
        _saveStatus = SettingsSaveState.Idle;
        _saving = true;
        try
        {
            await Task.WhenAll(
                UserPreferencesService.ResetUserVideoPlayerSettingsAsync(),
                UserPreferencesService.ResetUserVideoPlaybackPolicySettingsAsync(),
                UserPreferencesService.ResetUserTrackSelectionPreferencesAsync(_selectedLibraryId));
            _settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            _videoPolicy = await UserPreferencesService.GetEffectiveVideoPlaybackPolicySettingsAsync();
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync(_selectedLibraryId);
            _audioPassthrough = true;
            _exoBuffer = ExoVideoBufferSize.Auto;
            _hdmiAfr = _hdmiAfrDefault;
            _dvDecode = _dvDecodeDefault;
            ApplyMpcOptions(WindowsMpcPlaybackSettings.Empty());
            PersistDeviceVideoExperience();
            await ApplyLocalVideoPlayerSettingsAsync(_settings);
            CaptureFormState();
            await RefreshOverrideStateAsync();
            Snackbar.Add(L["ResetSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task RefreshOverrideStateAsync() =>
        _hasUserOverride = await UserPreferenceOverrideHelper.HasVideoOverridesAsync(
            UserPreferencesService,
            _selectedLibraryId);

    private WindowsMpcPlaybackOptions CurrentMpcOptions()
    {
        var path = _mpcExePath.Trim();
        if (_mpcEnabled && string.IsNullOrWhiteSpace(path))
            path = MpcExeLocator.TryFind() ?? "";

        if (!int.TryParse(_mpcWebPortText, out var port) || port is < 1 or > 65535)
            port = WindowsMpcPlaybackSettings.DefaultWebPort;

        return new WindowsMpcPlaybackOptions(
            _mpcEnabled,
            path,
            string.IsNullOrWhiteSpace(_mpcWebHost)
                ? WindowsMpcPlaybackSettings.DefaultWebHost
                : _mpcWebHost.Trim(),
            port,
            string.IsNullOrWhiteSpace(_mpcExtraArgs)
                ? WindowsMpcPlaybackSettings.DefaultExtraArgs
                : _mpcExtraArgs.Trim(),
            LibraryPathMirror.Serialize(_mpcLibraryLocalRoots));
    }

    private IEnumerable<LibraryDto> MpcMappableLibraries =>
        _libraries.Where(library =>
            library.PeerServerId is null
            && library.MediaType is LibraryMediaType.Movie or LibraryMediaType.Serie);

    private string LibraryLocalRoot(Guid libraryId) =>
        _mpcLibraryLocalRoots.GetValueOrDefault(libraryId) ?? "";

    private void SetLibraryLocalRoot(Guid libraryId, string? value)
    {
        _mpcLibraryLocalRoots[libraryId] = value ?? "";
        StateHasChanged();
    }

    private async Task BrowseLibraryFolderAsync(Guid libraryId)
    {
        if (!LocalPathPicker.CanPick)
            return;

        var picked = await LocalPathPicker.PickFolderAsync(LibraryLocalRoot(libraryId));
        if (!string.IsNullOrWhiteSpace(picked))
            SetLibraryLocalRoot(libraryId, picked);
    }

    private async Task BrowseMpcExeAsync()
    {
        if (!LocalPathPicker.CanPick)
            return;

        var picked = await LocalPathPicker.PickFileAsync([".exe"], _mpcExePath);
        if (!string.IsNullOrWhiteSpace(picked))
            _mpcExePath = picked;
    }

    private void ApplyMpcOptions(WindowsMpcPlaybackOptions options)
    {
        _mpcEnabled = options.Enabled;
        _mpcExePath = string.IsNullOrWhiteSpace(options.ExePath)
            ? MpcExeLocator.TryFind() ?? ""
            : options.ExePath;
        _mpcWebHost = options.WebHost;
        _mpcWebPortText = options.WebPort.ToString();
        _mpcExtraArgs = options.ExtraArgs;
        ApplyLibraryPathMap(LibraryPathMirror.Deserialize(options.LibraryLocalRootsJson));
    }

    private void ApplyLibraryPathMap(Dictionary<Guid, string> stored)
    {
        _mpcLibraryLocalRoots = MpcMappableLibraries.ToDictionary(
            library => library.Id,
            library => stored.GetValueOrDefault(library.Id) ?? "");
    }
}

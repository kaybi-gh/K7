using System.Globalization;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class PlaybackSettingsMenu : IDisposable
{
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Inject] private ILogger<PlaybackSettingsMenu> Logger { get; set; } = default!;

    private bool _open;
    private SettingsSection _activeSection = SettingsSection.None;
    private ElementReference _stackRef;
    private ElementReference _detailRef;
    private volatile bool _disposed;

    private static readonly double[] _playbackSpeedOptions = [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0];
    private static readonly AspectRatioMode[] _aspectRatioOptions = [AspectRatioMode.Fit, AspectRatioMode.Fill, AspectRatioMode.Stretch];

    protected enum SettingsSection
    {
        None,
        Audio,
        Subtitles,
        Quality,
        Speed,
        AspectRatio
    }

    private IReadOnlyList<SettingsSection> VisibleSections
    {
        get
        {
            var sections = new List<SettingsSection>();
            if (PlayerService.AudioTracks.Count > 1)
                sections.Add(SettingsSection.Audio);
            if (PlayerService.SubtitleTracks.Count > 0)
                sections.Add(SettingsSection.Subtitles);
            if (PlayerService.AvailableQualities.Count > 1)
                sections.Add(SettingsSection.Quality);
            sections.Add(SettingsSection.Speed);
            sections.Add(SettingsSection.AspectRatio);
            return sections;
        }
    }

    public bool TryHandleBack()
    {
        if (!_open)
            return false;

        if (_activeSection != SettingsSection.None)
        {
            _activeSection = SettingsSection.None;
            RequestStateHasChanged();
            return true;
        }

        CloseAsync().FireAndForget(Logger);
        return true;
    }

    protected override void OnParametersSet()
    {
        if (_open == Open)
            return;

        _open = Open;
        if (!_open)
            _activeSection = SettingsSection.None;
    }

    protected override void OnInitialized()
    {
        _open = Open;
        PlayerService.PlaybackRateChanged += OnPlaybackRateChanged;
        PlayerService.AudioTrackChanged += OnAudioTrackChanged;
        PlayerService.SubtitleTrackChanged += OnSubtitleTrackChanged;
        PlayerService.SubtitleTracksChanged += OnSubtitleTracksChanged;
        PlayerService.QualityChanged += OnQualityChanged;
        PlayerService.AspectRatioModeChanged += OnAspectRatioModeChanged;
        PlayerService.IsVisibleChanged += OnPlayerVisibilityChanged;
    }

    private void OnPlaybackRateChanged(double _) => RequestStateHasChanged();
    private void OnAudioTrackChanged(AudioFileTrackDto? _) => RequestStateHasChanged();
    private void OnSubtitleTrackChanged(SubtitleFileTrackDto? _) => RequestStateHasChanged();
    private void OnSubtitleTracksChanged() => RequestStateHasChanged();
    private void OnPlayerVisibilityChanged() => RequestStateHasChanged();
    private void OnQualityChanged(VideoQualityOption? _) => RequestStateHasChanged();
    private void OnAspectRatioModeChanged(AspectRatioMode _) => RequestStateHasChanged();

    private void RequestStateHasChanged()
    {
        if (_disposed)
            return;

        InvokeAsync(StateHasChanged).FireAndForget(Logger);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_open || _activeSection == SettingsSection.None)
            return;

        try
        {
            await JS.InvokeVoidAsync("K7.positionPlaybackSettingsDetail", _stackRef, _detailRef);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task ToggleAsync()
    {
        _open = !_open;
        if (!_open)
            _activeSection = SettingsSection.None;

        await OpenChanged.InvokeAsync(_open);
    }

    private async Task CloseAsync()
    {
        if (!_open)
            return;

        _open = false;
        _activeSection = SettingsSection.None;
        await OpenChanged.InvokeAsync(false);
    }

    private void SelectSection(SettingsSection section)
    {
        _activeSection = _activeSection == section ? SettingsSection.None : section;
    }

    private void BackToRoot() => _activeSection = SettingsSection.None;

    private int GetSectionIndex(SettingsSection section)
    {
        var sections = VisibleSections;
        for (var i = 0; i < sections.Count; i++)
        {
            if (sections[i] == section)
                return i;
        }

        return 0;
    }

    private static string GetSectionIcon(SettingsSection section) => section switch
    {
        SettingsSection.Audio => Phosphor.SpeakerHigh,
        SettingsSection.Subtitles => Phosphor.Subtitles,
        SettingsSection.Quality => Phosphor.SlidersHorizontal,
        SettingsSection.Speed => Phosphor.Gauge,
        SettingsSection.AspectRatio => Phosphor.FrameCorners,
        _ => Phosphor.Gear
    };

    private string GetSectionTitle(SettingsSection section) => section switch
    {
        SettingsSection.Audio => L["Audio"],
        SettingsSection.Subtitles => L["Subtitles"],
        SettingsSection.Quality => L["Quality"],
        SettingsSection.Speed => L["Speed"],
        SettingsSection.AspectRatio => L["AspectRatio"],
        _ => L["PlaybackSettings"]
    };

    private void OnSpeedSelected(double speed) => PlayerService.SetPlaybackRate(speed);

    private bool IsCurrentSpeed(double speed) => Math.Abs(PlayerService.PlaybackRate - speed) < 0.01;

    private void OnAspectRatioSelected(AspectRatioMode mode) => PlayerService.SetAspectRatioMode(mode);

    private string GetAspectRatioLabel(AspectRatioMode mode) => mode switch
    {
        AspectRatioMode.Fit => L["AspectRatioFit"],
        AspectRatioMode.Fill => L["AspectRatioFillCrop"],
        AspectRatioMode.Stretch => L["AspectRatioStretch"],
        _ => mode.ToString()
    };

    private async Task OnAudioTrackSelected(AudioFileTrackDto track)
    {
        if (PlayerService.SelectedAudioTrack?.Index == track.Index)
            return;

        await PlayerService.ChangeAudioTrackAsync(track);
    }

    private async Task OnSubtitleTrackSelected(SubtitleFileTrackDto? track)
    {
        if (PlayerService.SelectedSubtitleTrack?.Index == track?.Index)
            return;

        await PlayerService.ChangeSubtitleTrackAsync(track);
    }

    private async Task OnQualitySelected(VideoQualityOption quality)
    {
        if (PlayerService.SelectedQuality == quality)
            return;

        await PlayerService.ChangeQualityAsync(quality);
    }

    private string GetAudioTrackLabel(AudioFileTrackDto track)
    {
        var language = GetTranslatedLanguageName(track.Language ?? "und");
        var channels = track.ChannelLayout?.Split('(')[0].Trim();
        var details = !string.IsNullOrEmpty(channels)
            ? $"{track.Codec} {channels}"
            : track.Codec;

        if (!string.IsNullOrEmpty(track.Name)
            && !string.Equals(track.Name, track.Language, StringComparison.OrdinalIgnoreCase)
            && !IsLanguageCode(track.Name)
            && !track.Name.Contains(track.Codec ?? "", StringComparison.OrdinalIgnoreCase))
        {
            return $"{language} - {track.Name} ({details})";
        }

        return $"{language} ({details})";
    }

    private string GetSubtitleTrackLabel(SubtitleFileTrackDto track)
    {
        var language = GetTranslatedLanguageName(track.Language ?? "und");
        var type = track.IsHearingImpaired
            ? L["SubtitleTypeHearingImpaired"]
            : track.IsForced
                ? L["SubtitleTypeForced"]
                : L["SubtitleTypeFull"];
        return $"{language} - {type} ({track.Codec})";
    }

    private static string GetTranslatedLanguageName(string code)
    {
        if (string.IsNullOrEmpty(code) || code == "und")
            return code;

        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            if (!string.IsNullOrEmpty(culture.DisplayName) && culture.DisplayName != code)
                return char.ToUpper(culture.DisplayName[0]) + culture.DisplayName[1..];
        }
        catch
        {
            // CultureInfo doesn't recognize this code
        }

        return SupportedLanguages.GetDisplayLabel(code);
    }

    private static bool IsLanguageCode(string value)
    {
        return value.Length is 2 or 3
            && value.All(c => c is >= 'a' and <= 'z');
    }

    public void Dispose()
    {
        _disposed = true;
        PlayerService.PlaybackRateChanged -= OnPlaybackRateChanged;
        PlayerService.AudioTrackChanged -= OnAudioTrackChanged;
        PlayerService.SubtitleTrackChanged -= OnSubtitleTrackChanged;
        PlayerService.SubtitleTracksChanged -= OnSubtitleTracksChanged;
        PlayerService.QualityChanged -= OnQualityChanged;
        PlayerService.AspectRatioModeChanged -= OnAspectRatioModeChanged;
        PlayerService.IsVisibleChanged -= OnPlayerVisibilityChanged;
    }
}

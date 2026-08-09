using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.Maui.Controls.Shapes;

namespace K7.Clients.MAUI.Controls.Video;

public enum NativeSettingsPage
{
    Root,
    Audio,
    Subtitles,
    Quality,
    Speed,
    Aspect
}

/// <summary>
/// Two-level playback settings panel (audio / subs / quality / speed / aspect).
/// Visual + TV focus parity with Blazor <c>PlaybackSettingsMenu</c>.
/// </summary>
public sealed class NativePlaybackSettingsPanel : Border
{
    private readonly IPlayerService _player;
    private readonly VerticalStackLayout _list = new() { Spacing = 0 };
    private readonly ScrollView _scroll;
    private readonly Grid _root = new();
    private readonly Label _titleLabel = new();
    private readonly Button _headerActionButton = new();
    private readonly List<Row> _rows = [];
    private NativeSettingsPage _page = NativeSettingsPage.Root;
    private NativeSettingsPage? _builtPage;
    private int _focusedIndex = -1;

    public event EventHandler? Closed;
    public event EventHandler? OpenedChanged;

    public bool IsOpen { get; private set; }

    public NativePlaybackSettingsPanel(IPlayerService player)
    {
        _player = player;
        IsVisible = false;
        BackgroundColor = Color.FromArgb("#F2121212");
        Stroke = Color.FromArgb("#33FFFFFF");
        StrokeThickness = 1;
        StrokeShape = new RoundRectangle { CornerRadius = 10 };
        Padding = 0;
        WidthRequest = 280;
        // Height hugs content; MaximumHeightRequest is set by the overlay to
        // (screen - bottom chrome) so long lists scroll instead of growing forever.

        _titleLabel.TextColor = Colors.White;
        _titleLabel.FontSize = 14;
        _titleLabel.FontAttributes = FontAttributes.Bold;
        _titleLabel.VerticalOptions = LayoutOptions.Center;
        _titleLabel.LineBreakMode = LineBreakMode.TailTruncation;
        _titleLabel.HorizontalOptions = LayoutOptions.Fill;

        StyleHeaderButton(_headerActionButton);
        _headerActionButton.Clicked += (_, _) =>
        {
            if (_page == NativeSettingsPage.Root)
                Close();
            else
            {
                _page = NativeSettingsPage.Root;
                Rebuild();
            }
        };

        var header = new Grid
        {
            Padding = new Thickness(8, 8, 8, 8),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            MinimumHeightRequest = 44
        };
        Grid.SetColumn(_headerActionButton, 0);
        Grid.SetColumn(_titleLabel, 1);
        header.Children.Add(_headerActionButton);
        header.Children.Add(_titleLabel);

        var headerRule = new BoxView
        {
            Color = Color.FromArgb("#33FFFFFF"),
            HeightRequest = 1
        };

        var headerStack = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { header, headerRule }
        };

        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        _list.Padding = new Thickness(4, 4, 4, 8);
        _scroll = new ScrollView
        {
            Content = _list,
            Orientation = ScrollOrientation.Vertical,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalScrollBarVisibility = ScrollBarVisibility.Default
        };
        Grid.SetRow(headerStack, 0);
        Grid.SetRow(_scroll, 1);
        _root.Children.Add(headerStack);
        _root.Children.Add(_scroll);

        Content = _root;
    }

    /// <summary>
    /// Caps the panel to the space above the bottom transport bar. Content shorter than
    /// <paramref name="availableHeight"/> keeps its natural height; longer content scrolls.
    /// </summary>
    public void SetAvailableHeight(double availableHeight)
    {
        if (availableHeight <= 0)
            return;

        MaximumHeightRequest = availableHeight;
        // Clear any forced height so short menus do not leave empty space.
        HeightRequest = -1;
    }

    public void Open()
    {
        _page = NativeSettingsPage.Root;
        IsOpen = true;
        IsVisible = true;
        // Prefer the pre-warmed root list from SetActive; only rebuild when empty or stale.
        if (_rows.Count == 0 || _builtPage != NativeSettingsPage.Root)
            Rebuild();
        else
        {
            UpdateHeader();
            SetFocusedIndex(0);
        }

        OpenedChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        IsVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);
        OpenedChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryHandleBack()
    {
        if (!IsOpen)
            return false;

        if (_page != NativeSettingsPage.Root)
        {
            _page = NativeSettingsPage.Root;
            Rebuild();
            return true;
        }

        Close();
        return true;
    }

    public bool MoveFocus(int direction)
    {
        if (!IsOpen || _rows.Count == 0)
            return false;

        var next = NativeSettingsFocusNavigator.MoveFocus(_focusedIndex, _rows.Count, direction);
        SetFocusedIndex(next);
        return true;
    }

    public bool ActivateFocused()
    {
        if (!IsOpen || _focusedIndex < 0 || _focusedIndex >= _rows.Count)
            return false;

        _rows[_focusedIndex].Activate();
        return true;
    }

    public void Rebuild()
    {
        _list.Children.Clear();
        _rows.Clear();
        _focusedIndex = -1;
        UpdateHeader();
        switch (_page)
        {
            case NativeSettingsPage.Root:
                BuildRoot();
                break;
            case NativeSettingsPage.Audio:
                BuildAudio();
                break;
            case NativeSettingsPage.Subtitles:
                BuildSubtitles();
                break;
            case NativeSettingsPage.Quality:
                BuildQuality();
                break;
            case NativeSettingsPage.Speed:
                BuildSpeed();
                break;
            case NativeSettingsPage.Aspect:
                BuildAspect();
                break;
        }

        _builtPage = _page;
        SetFocusedIndex(_rows.Count > 0 ? 0 : -1);
    }

    private void UpdateHeader()
    {
        if (_page == NativeSettingsPage.Root)
        {
            _titleLabel.Text = NativeStrings.PlaybackSettings;
            _headerActionButton.Text = NativePlayerGlyphs.Close;
            _headerActionButton.IsVisible = true;
        }
        else
        {
            _titleLabel.Text = _page switch
            {
                NativeSettingsPage.Audio => NativeStrings.Audio,
                NativeSettingsPage.Subtitles => NativeStrings.Subtitles,
                NativeSettingsPage.Quality => NativeStrings.Quality,
                NativeSettingsPage.Speed => NativeStrings.Speed,
                NativeSettingsPage.Aspect => NativeStrings.AspectRatio,
                _ => NativeStrings.PlaybackSettings
            };
            _headerActionButton.Text = NativePlayerGlyphs.CaretLeft;
            _headerActionButton.IsVisible = true;
        }
    }

    private void BuildRoot()
    {
        if (_player.AudioTracks.Count > 1)
            AddNavRow(NativePlayerGlyphs.SpeakerHigh, NativeStrings.Audio, () => { _page = NativeSettingsPage.Audio; Rebuild(); });
        if (_player.SubtitleTracks.Count > 0)
            AddNavRow(NativePlayerGlyphs.Subtitles, NativeStrings.Subtitles, () => { _page = NativeSettingsPage.Subtitles; Rebuild(); });
        if (_player.AvailableQualities.Count > 1)
            AddNavRow(NativePlayerGlyphs.SlidersHorizontal, NativeStrings.Quality, () => { _page = NativeSettingsPage.Quality; Rebuild(); });
        AddNavRow(NativePlayerGlyphs.Gauge, NativeStrings.Speed, () => { _page = NativeSettingsPage.Speed; Rebuild(); });
        AddNavRow(NativePlayerGlyphs.FrameCorners, NativeStrings.AspectRatio, () => { _page = NativeSettingsPage.Aspect; Rebuild(); });
    }

    private void BuildAudio()
    {
        foreach (var track in _player.AudioTracks)
        {
            var selected = ReferenceEquals(track, _player.SelectedAudioTrack)
                || (_player.SelectedAudioTrack is not null && track.Index == _player.SelectedAudioTrack.Index);
            AddSelectRow(FormatAudio(track), selected, () => _ = _player.ChangeAudioTrackAsync(track));
        }
    }

    private void BuildSubtitles()
    {
        AddSelectRow(NativeStrings.SubtitlesOff, _player.SelectedSubtitleTrack is null, () => _ = _player.ChangeSubtitleTrackAsync(null));
        foreach (var track in _player.SubtitleTracks)
        {
            var selected = _player.SelectedSubtitleTrack is not null && track.Index == _player.SelectedSubtitleTrack.Index;
            AddSelectRow(FormatSubtitle(track), selected, () => _ = _player.ChangeSubtitleTrackAsync(track));
        }
    }

    private void BuildQuality()
    {
        foreach (var quality in _player.AvailableQualities)
        {
            var selected = _player.SelectedQuality?.Label == quality.Label;
            AddSelectRow(quality.Label, selected, () => _ = _player.ChangeQualityAsync(quality));
        }
    }

    private void BuildSpeed()
    {
        double[] speeds = [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0];
        foreach (var speed in speeds)
        {
            var selected = Math.Abs(_player.PlaybackRate - speed) < 0.01;
            var label = Math.Abs(speed - 1.0) < 0.01 ? NativeStrings.Normal : $"{speed:0.##}x";
            AddSelectRow(label, selected, () => _player.SetPlaybackRate(speed));
        }
    }

    private void BuildAspect()
    {
        AddSelectRow(NativeStrings.Fit, _player.AspectRatio == AspectRatioMode.Fit, () => _player.SetAspectRatioMode(AspectRatioMode.Fit));
        AddSelectRow(NativeStrings.Fill, _player.AspectRatio == AspectRatioMode.Fill, () => _player.SetAspectRatioMode(AspectRatioMode.Fill));
        AddSelectRow(NativeStrings.Stretch, _player.AspectRatio == AspectRatioMode.Stretch, () => _player.SetAspectRatioMode(AspectRatioMode.Stretch));
    }

    private void AddNavRow(string glyph, string text, Action action)
    {
        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };
        var icon = new Label
        {
            Text = glyph,
            FontFamily = NativePlayerGlyphs.FontFamily,
            TextColor = Colors.White,
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center,
            FontAutoScalingEnabled = false
        };
        var label = new Label
        {
            Text = text,
            TextColor = Colors.White,
            FontSize = 15,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            FontAutoScalingEnabled = false
        };
        var chevron = new Label
        {
            Text = NativePlayerGlyphs.CaretRight,
            FontFamily = NativePlayerGlyphs.FontFamily,
            TextColor = Color.FromArgb("#99FFFFFF"),
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            FontAutoScalingEnabled = false
        };
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(label, 1);
        Grid.SetColumn(chevron, 2);
        content.Children.Add(icon);
        content.Children.Add(label);
        content.Children.Add(chevron);
        AddRowView(content, selected: false, action);
    }

    private void AddSelectRow(string text, bool selected, Action action)
    {
        var content = NativeIconText.CreateContent(
            selected ? NativePlayerGlyphs.CheckCircle : null,
            text);
        AddRowView(content, selected, () =>
        {
            action();
            Rebuild();
        });
    }

    private void AddRowView(View content, bool selected, Action action)
    {
        var border = new Border
        {
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            BackgroundColor = selected ? Color.FromArgb("#33FFFFFF") : Colors.Transparent,
            Padding = new Thickness(12, 11),
            Content = content,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(4, 2)
        };

        var row = new Row(border, action) { Selected = selected };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => row.Activate();
        border.GestureRecognizers.Add(tap);
        _rows.Add(row);
        _list.Children.Add(border);
    }

    private void SetFocusedIndex(int index)
    {
        if (_focusedIndex >= 0 && _focusedIndex < _rows.Count)
            ApplyRowVisual(_rows[_focusedIndex], focused: false);

        _focusedIndex = NativeSettingsFocusNavigator.ClampFocus(index, _rows.Count);

        if (_focusedIndex >= 0 && _focusedIndex < _rows.Count)
        {
            ApplyRowVisual(_rows[_focusedIndex], focused: true);
            _ = ScrollFocusedRowIntoViewAsync();
        }
    }

    private async Task ScrollFocusedRowIntoViewAsync()
    {
        if (_focusedIndex < 0 || _focusedIndex >= _rows.Count)
            return;

        var view = _rows[_focusedIndex].View;
        try
        {
            // MakeVisible keeps neighbouring rows on-screen while D-pad walks a long list.
            await _scroll.ScrollToAsync(view, ScrollToPosition.MakeVisible, animated: false);
        }
        catch
        {
            // Layout may not be ready on the first Open frame.
        }
    }

    private static void ApplyRowVisual(Row row, bool focused)
    {
        if (focused)
        {
            row.View.BackgroundColor = Color.FromArgb("#66FFFFFF");
            row.View.Stroke = Colors.White;
            row.View.StrokeThickness = 2;
        }
        else
        {
            row.View.Stroke = Colors.Transparent;
            row.View.StrokeThickness = 0;
            row.View.BackgroundColor = row.Selected
                ? Color.FromArgb("#33FFFFFF")
                : Colors.Transparent;
        }
    }

    private static void StyleHeaderButton(Button button)
    {
        button.BackgroundColor = Colors.Transparent;
        button.TextColor = Color.FromArgb("#CCFFFFFF");
        button.FontFamily = NativePlayerGlyphs.FontFamily;
        button.FontSize = 18;
        button.WidthRequest = 36;
        button.HeightRequest = 36;
        button.Padding = 0;
        button.BorderWidth = 0;
        button.FontAutoScalingEnabled = false;
    }

    private static string FormatAudio(AudioFileTrackDto track)
    {
        var lang = string.IsNullOrWhiteSpace(track.Language) ? "Unknown" : track.Language;
        var codec = string.IsNullOrWhiteSpace(track.Codec) ? "" : $" - {track.Codec}";
        var flag = NativeLanguageFlags.GetFlagEmoji(track.Language);
        return string.IsNullOrEmpty(flag) ? $"{lang}{codec}" : $"{flag} {lang}{codec}";
    }

    private static string FormatSubtitle(SubtitleFileTrackDto track)
    {
        var lang = string.IsNullOrWhiteSpace(track.Language) ? "Unknown" : track.Language;
        var name = string.IsNullOrWhiteSpace(track.Name) ? "" : $" - {track.Name}";
        var flag = NativeLanguageFlags.GetFlagEmoji(track.Language);
        return string.IsNullOrEmpty(flag) ? $"{lang}{name}" : $"{flag} {lang}{name}";
    }

    private sealed class Row(Border view, Action activate)
    {
        public Border View { get; } = view;
        public bool Selected { get; set; }
        public void Activate() => activate();
    }
}

/// <summary>
/// Font-family independent flag fallback for the native settings panel: no flag-icons SVG
/// assets are bundled in MAUI, so ISO 3166-1 alpha-2 country codes map to Unicode regional
/// indicator flag emoji (rendered by the OS emoji font, not Phosphor).
/// </summary>
public static class NativeLanguageFlags
{
    public static string? GetFlagEmoji(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return null;

        var option = SupportedLanguages.FindByCode(languageCode);
        var countryCode = option?.CountryCode;
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
            return null;

        var upper = countryCode.ToUpperInvariant();
        const int regionalIndicatorBase = 0x1F1E6 - 'A';
        var first = char.ConvertFromUtf32(regionalIndicatorBase + upper[0]);
        var second = char.ConvertFromUtf32(regionalIndicatorBase + upper[1]);
        return first + second;
    }
}

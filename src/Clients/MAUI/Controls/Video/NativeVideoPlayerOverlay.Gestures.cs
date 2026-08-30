namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Touch gestures on phone/tablet: left half vertical drag = brightness + dim overlay,
/// right half = volume + swipe bar, double-tap left/right = skip with ripple. Mirrors the
/// touch handlers in <c>VideoPlayerControlsOverlay.razor(.cs)</c> (OnTouchStart/Move/End,
/// brightness-overlay, swipe-indicator).
/// </summary>
public sealed partial class NativeVideoPlayerOverlay
{
    private enum SwipeSide { Left, Right }

    private readonly BoxView _brightnessDimOverlay = new() { Color = Colors.Black, InputTransparent = true, Opacity = 0 };
    private readonly Border _swipeIndicator = new() { InputTransparent = true, IsVisible = false };
    private readonly Label _swipeIcon = new();
    private readonly Label _swipeLabel = new();
    private readonly BoxView _swipeBarFill = new() { Color = Colors.White };
    private readonly Border _rippleLeft = new() { InputTransparent = true, IsVisible = false };
    private readonly Border _rippleRight = new() { InputTransparent = true, IsVisible = false };

    private double _panStartBrightness;
    private double _panStartVolume;
    private bool _panOriginCaptured;
    private System.Timers.Timer? _rippleTimer;

    private void BuildGestureVisuals()
    {
        Children.Add(_brightnessDimOverlay);

        _swipeIcon.TextColor = Colors.White;
        _swipeIcon.FontFamily = NativePlayerGlyphs.FontFamily;
        _swipeIcon.FontSize = 24;
        _swipeIcon.HorizontalOptions = LayoutOptions.Center;

        _swipeLabel.TextColor = Colors.White;
        _swipeLabel.FontSize = 13;
        _swipeLabel.HorizontalOptions = LayoutOptions.Center;

        var barContainer = new Grid { WidthRequest = 6, HeightRequest = 100, BackgroundColor = Color.FromArgb("#33FFFFFF") };
        barContainer.Children.Add(new BoxView { Color = Colors.Transparent });
        _swipeBarFill.VerticalOptions = LayoutOptions.End;
        _swipeBarFill.HeightRequest = 0;
        barContainer.Children.Add(_swipeBarFill);

        var swipeStack = new VerticalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
        swipeStack.Children.Add(_swipeIcon);
        swipeStack.Children.Add(barContainer);
        swipeStack.Children.Add(_swipeLabel);

        _swipeIndicator.Content = swipeStack;
        _swipeIndicator.BackgroundColor = Color.FromArgb("#66000000");
        _swipeIndicator.Padding = new Thickness(16, 12);
        _swipeIndicator.HorizontalOptions = LayoutOptions.Center;
        _swipeIndicator.VerticalOptions = LayoutOptions.Center;
        Children.Add(_swipeIndicator);

        BuildRipple(_rippleLeft, LayoutOptions.Start);
        BuildRipple(_rippleRight, LayoutOptions.End);
        Children.Add(_rippleLeft);
        Children.Add(_rippleRight);
    }

    private static void BuildRipple(Border ripple, LayoutOptions horizontal)
    {
        ripple.Content = new Label { Text = string.Empty, FontFamily = NativePlayerGlyphs.FontFamily, TextColor = Colors.White, FontSize = 22, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        ripple.BackgroundColor = Color.FromArgb("#33FFFFFF");
        ripple.WidthRequest = 90;
        ripple.HeightRequest = 90;
        ripple.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 45 };
        ripple.Stroke = Colors.Transparent;
        ripple.HorizontalOptions = horizontal;
        ripple.VerticalOptions = LayoutOptions.Center;
        ripple.Margin = new Thickness(40);
        ripple.Opacity = 0;
    }

    private void OnDoubleTapped(bool isRight)
    {
        if (!IsPhoneOrTablet() || IsNextEpisodeVisible)
            return;

        var delta = isRight ? _player.SkipForwardSeconds : -_player.SkipBackSeconds;
        _player.Seek(Math.Clamp(_player.CurrentTime + delta, 0, Math.Max(_player.Duration, 0)));
        ShowHud(
            NativeTimeFormatting.Format(_player.CurrentTime),
            delta >= 0 ? NativePlayerGlyphs.Forward : NativePlayerGlyphs.Rewind);
        ShowRipple(isRight);
    }

    private void ShowRipple(bool right)
    {
        var ripple = right ? _rippleRight : _rippleLeft;
        ((Label)ripple.Content!).Text = right ? NativePlayerGlyphs.Forward : NativePlayerGlyphs.Rewind;
        ripple.IsVisible = true;
        ripple.Opacity = 1;
        ripple.Scale = 0.8;
        _ = ripple.FadeToAsync(0, 500);
        _ = ripple.ScaleToAsync(1.15, 500);

        _rippleTimer?.Stop();
        _rippleTimer?.Dispose();
        _rippleTimer = new System.Timers.Timer(500) { AutoReset = false };
        _rippleTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(() => ripple.IsVisible = false);
        _rippleTimer.Start();
    }

    private void OnPanUpdated(PanUpdatedEventArgs e, bool panSideLeft)
    {
        if (!IsPhoneOrTablet() || IsNextEpisodeVisible)
            return;

        // Vertical scrub only; ignore mostly-horizontal pans.
        if (e.StatusType == GestureStatus.Running && Math.Abs(e.TotalX) > Math.Abs(e.TotalY) + 20)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                CapturePanOrigin();
                break;
            case GestureStatus.Running:
            {
                if (!_panOriginCaptured)
                    CapturePanOrigin();

                var side = panSideLeft ? SwipeSide.Left : SwipeSide.Right;

                if (side == SwipeSide.Left)
                {
                    if (_brightness is null)
                        return;

                    _swipeIndicator.IsVisible = true;
                    var next = Math.Clamp(_panStartBrightness - e.TotalY / 600, 0, 1);
                    _brightness.SetBrightness(next);
                    if (!_brightness.SupportsNativeBrightness)
                        _brightnessDimOverlay.Opacity = 1.0 - next;
                    UpdateSwipeIndicator(SwipeSide.Left, NativePlayerGlyphs.Sun, next);
                }
                else
                {
                    _swipeIndicator.IsVisible = true;
                    var next = Math.Clamp(_panStartVolume - e.TotalY / 600, 0, 1);
                    ApplyUserVolume(next);

                    UpdateSwipeIndicator(SwipeSide.Right, NativePlayerGlyphs.SpeakerHigh, next);
                    UpdateTransport();
                }

                break;
            }
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _panOriginCaptured = false;
                _swipeIndicator.IsVisible = false;
                break;
        }
    }

    private void CapturePanOrigin()
    {
        _panStartBrightness = _brightness?.Brightness ?? 1;
        _panStartVolume = _volumeService?.SupportsNativeVolume == true
            ? _volumeService.Volume
            : (_player.IsMuted ? 0 : _player.Volume);
        _panOriginCaptured = true;
    }

    private void UpdateSwipeIndicator(SwipeSide side, string icon, double percent)
    {
        _swipeIcon.Text = icon;
        _swipeLabel.Text = $"{(int)Math.Round(percent * 100)}%";
        _swipeBarFill.HeightRequest = 100 * percent;
        _swipeIndicator.HorizontalOptions = side == SwipeSide.Left ? LayoutOptions.Start : LayoutOptions.End;
        _swipeIndicator.Margin = side == SwipeSide.Left ? new Thickness(24, 0, 0, 0) : new Thickness(0, 0, 24, 0);
    }
}

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
    private double _panStartX;
    private bool _panSideLeft;
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

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(_tapCatcher);
        if (pos is null)
            return;

        _panStartX = pos.Value.X;
        var width = _tapCatcher.Width > 0 ? _tapCatcher.Width : Width;
        if (width <= 0)
            width = 400;
        _panSideLeft = _panStartX < width / 2;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!IsPhoneOrTablet())
            return;

        var pos = e.GetPosition(_tapCatcher);
        if (pos is null)
            return;

        var width = _tapCatcher.Width > 0 ? _tapCatcher.Width : Width;
        if (width <= 0)
            width = 400;

        var isRight = pos.Value.X >= width / 2;
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

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!IsPhoneOrTablet())
            return;

        // Vertical scrub only; ignore mostly-horizontal pans.
        if (e.StatusType == GestureStatus.Running && Math.Abs(e.TotalX) > Math.Abs(e.TotalY) + 20)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartBrightness = _brightness?.Brightness ?? 1;
                _panStartVolume = _volumeService?.SupportsNativeVolume == true
                    ? _volumeService.Volume
                    : (_player.IsMuted ? 0 : _player.Volume);
                break;
            case GestureStatus.Running:
            {
                _swipeIndicator.IsVisible = true;
                var side = _panSideLeft ? SwipeSide.Left : SwipeSide.Right;

                if (side == SwipeSide.Left && _brightness is not null)
                {
                    var next = Math.Clamp(_panStartBrightness - e.TotalY / 600, 0.05, 1);
                    _brightness.SetBrightness(next);
                    if (!_brightness.SupportsNativeBrightness)
                        _brightnessDimOverlay.Opacity = 1.0 - next;
                    UpdateSwipeIndicator(SwipeSide.Left, NativePlayerGlyphs.Sun, next);
                }
                else if (side == SwipeSide.Right)
                {
                    var next = Math.Clamp(_panStartVolume - e.TotalY / 600, 0, 1);
                    if (_volumeService is not null)
                        _volumeService.SetVolume(next);
                    else
                        _player.SetVolume(next);
                    if (next <= 0)
                        _player.Mute();
                    else if (_player.IsMuted)
                        _player.Unmute();
                    UpdateSwipeIndicator(SwipeSide.Right, NativePlayerGlyphs.SpeakerHigh, next);
                    UpdateTransport();
                }

                break;
            }
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _swipeIndicator.IsVisible = false;
                break;
        }
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

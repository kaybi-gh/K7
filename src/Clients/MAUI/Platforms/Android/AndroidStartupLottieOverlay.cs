using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using SkiaSharp;
using SkiaSharp.Views.Android;
using AActivity = Android.App.Activity;
using AColor = Android.Graphics.Color;
using SkottieAnimation = SkiaSharp.Skottie.Animation;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Lottie on the activity DecorView so it stays above WebView / MediaElement surfaces
/// and is not disposed when Window.Page is replaced.
/// </summary>
internal static class AndroidStartupLottieOverlay
{
    private static FrameLayout? _root;
    private static ImageView? _logo;
    private static SplashSkottieView? _lottie;
    private static bool _readySignaled;

    public static bool IsShown => _root is not null;

    public static event Action? ReadyToBuildStartPage;

    public static void Show(AActivity activity)
    {
        if (_root is not null || activity.Window?.DecorView is not ViewGroup decor)
            return;

        _readySignaled = false;
        _root = new FrameLayout(activity);
        _root.SetBackgroundColor(AColor.ParseColor("#0d0907"));
        _root.Elevation = 10000f;
        _root.Clickable = true;
        _root.Focusable = true;

        var logoId = activity.Resources?.GetIdentifier("k7_logo", "drawable", activity.PackageName) ?? 0;
        if (logoId != 0)
        {
            _logo = new ImageView(activity);
            _logo.SetImageResource(logoId);
            _logo.SetScaleType(ImageView.ScaleType.FitCenter);
            _logo.Elevation = 0f;
            _root.AddView(_logo, CenteredLogoParams(activity));
        }

        _lottie = new SplashSkottieView(activity);
        _lottie.Elevation = 8f;
        _lottie.FirstFramePainted += OnFirstFramePainted;
        _root.AddView(_lottie, CenteredLogoParams(activity));
        _lottie.BringToFront();

        decor.AddView(
            _root,
            new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent));
        _root.BringToFront();
    }

    public static void ResumeTicker() => _lottie?.ResumeTicker();

    public static void Dismiss()
    {
        if (_root is null)
            return;

        if (_lottie is not null)
            _lottie.FirstFramePainted -= OnFirstFramePainted;

        (_root.Parent as ViewGroup)?.RemoveView(_root);
        _lottie?.Teardown();
        _lottie = null;
        _logo = null;
        _root = null;
        ReadyToBuildStartPage = null;
    }

    private static void OnFirstFramePainted()
    {
        HideStaticLogo();
        _lottie?.RestartPlayback();
        // Wait until the reveal has reached the hold pose before BlazorPage ctor
        // freezes the UI thread (~1.6s MediaElements).
        var playMs = _lottie is { DurationMs: > 400 } ? _lottie.DurationMs - 400 : 2600;
        _root?.PostDelayed(SignalReadyToBuildStartPage, playMs);
    }

    private static void SignalReadyToBuildStartPage()
    {
        if (_readySignaled)
            return;

        _readySignaled = true;
        ReadyToBuildStartPage?.Invoke();
    }

    private static void HideStaticLogo()
    {
        if (_logo is null || _logo.Visibility != ViewStates.Visible)
            return;

        _logo.Visibility = ViewStates.Gone;
    }

    private static FrameLayout.LayoutParams CenteredLogoParams(Context context)
    {
        var px = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 120, context.Resources!.DisplayMetrics);
        return new FrameLayout.LayoutParams(px, px)
        {
            Gravity = GravityFlags.Center
        };
    }
}

internal sealed class SplashSkottieView : SKCanvasView
{
    private readonly SkottieAnimation? _animation;
    private readonly Handler _ticker = new(Looper.MainLooper!);
    private Java.Lang.IRunnable? _tickRunnable;
    private readonly System.Diagnostics.Stopwatch _watch = new();
    private bool _running = true;
    private bool _firstFrameRaised;
    private bool _tickerPosted;

    public SplashSkottieView(Context context)
        : base(context)
    {
        SetLayerType(LayerType.Software, null);
        SetBackgroundColor(AColor.Transparent);
        _animation = LoadAnimation();
        PaintSurface += OnPaint;
        _tickRunnable = new Java.Lang.Runnable(Tick);
    }

    public event Action? FirstFramePainted;

    public bool HasAnimation => _animation is not null;

    public long DurationMs => _animation is null ? 0 : (long)_animation.Duration.TotalMilliseconds;

    public void RestartPlayback()
    {
        _watch.Restart();
        ResumeTicker();
    }

    public void ResumeTicker()
    {
        if (!_running)
            return;

        _tickerPosted = true;
        if (_tickRunnable is null)
            return;

        _ticker.RemoveCallbacks(_tickRunnable);
        _ticker.Post(_tickRunnable);
    }

    public void Teardown()
    {
        _running = false;
        _tickerPosted = false;
        if (_tickRunnable is not null)
            _ticker.RemoveCallbacks(_tickRunnable);
        PaintSurface -= OnPaint;
        FirstFramePainted = null;
        _animation?.Dispose();
    }

    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        ResumeTicker();
    }

    private void Tick()
    {
        if (!_running || !_tickerPosted)
            return;

        Invalidate();
        if (_tickRunnable is not null)
            _ticker.PostDelayed(_tickRunnable, 16);
    }

    private static SkottieAnimation? LoadAnimation()
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("splash.json").GetAwaiter().GetResult();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            using var data = SKData.CreateCopy(ms.ToArray());
            return SkottieAnimation.Create(data);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void OnPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (_animation is not null)
        {
            var duration = _animation.Duration;
            if (duration > TimeSpan.Zero)
            {
                var t = _watch.IsRunning ? _watch.Elapsed : TimeSpan.Zero;
                if (t > duration)
                    t = duration;
                _animation.SeekFrameTime(t);
            }

            _animation.Render(canvas, new SKRect(0, 0, e.Info.Width, e.Info.Height));
        }

        if (!_firstFrameRaised)
        {
            _firstFrameRaised = true;
            FirstFramePainted?.Invoke();
        }
    }
}

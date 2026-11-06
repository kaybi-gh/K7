using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using K7.Clients.Shared.Helpers;
using SkiaSharp;
using AActivity = Android.App.Activity;
using AColor = Android.Graphics.Color;
using SkottieAnimation = SkiaSharp.Skottie.Animation;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Lottie on the activity DecorView so it stays above WebView / MediaElement surfaces
/// and is not disposed when Window.Page is replaced.
/// Renders via Skottie -&gt; SKBitmap -&gt; ImageView. SKCanvasView PaintSurface often
/// never fires on Android TV after the SkiaSharp 4 bump.
/// </summary>
internal static class AndroidStartupLottieOverlay
{
    // splash.json is 128x70. A square box stretched the mark and Android 12
    // then clipped it to a circle (sides cut off).
    private const int LogoWidthDip = 220;
    private const int LogoHeightDip = 120;

    private static FrameLayout? _root;
    private static ImageView? _logo;
    private static ImageView? _lottieView;
    private static SplashSkottieDriver? _driver;
    private static bool _readySignaled;
    private static bool _logoHidden;
    private static int _loadGeneration;
    /// <summary>
    /// Sticky: once BlazorWebView is up, keep Skottie at ~10fps until dismiss.
    /// </summary>
    private static bool _throttleForBlazor;
    private static long _shownAtMs;
    private static bool _fading;
    private const int FadeDurationMs = 350;

    public static bool IsShown => _root is not null;

    public static bool HasAnimation => _driver?.HasAnimation == true;

    /// <summary>Elapsed since <see cref="Show"/>, or zero if not shown.</summary>
    public static TimeSpan TimeSinceShown =>
        _shownAtMs == 0
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(Java.Lang.JavaSystem.CurrentTimeMillis() - _shownAtMs);

    public static event Action? ReadyToBuildStartPage;

    public static void Show(AActivity activity)
    {
        if (_root is not null || activity.Window?.DecorView is not ViewGroup decor)
            return;

        global::K7.Clients.MAUI.MauiStartupTrace.Mark("SplashOverlay-Show-start");

        _readySignaled = false;
        _logoHidden = false;
        _throttleForBlazor = false;
        _shownAtMs = Java.Lang.JavaSystem.CurrentTimeMillis();
        _root = new FrameLayout(activity);
        _root.SetBackgroundColor(AColor.ParseColor("#0d0907"));
        _root.Elevation = 10000f;
        _root.Clickable = true;
        _root.Focusable = true;

        // Static mark first so cold start is not a long blank brand color.
        var logoId = activity.Resources?.GetIdentifier("k7_logo", "drawable", activity.PackageName) ?? 0;
        if (logoId != 0)
        {
            _logo = new ImageView(activity);
            _logo.SetImageResource(logoId);
            _logo.SetScaleType(ImageView.ScaleType.FitCenter);
            _logo.Elevation = 0f;
            _root.AddView(_logo, CenteredLogoParams(activity));
            global::K7.Clients.MAUI.MauiStartupTrace.Mark("SplashOverlay-LogoShown");
        }
        else
        {
            global::K7.Clients.MAUI.MauiStartupTrace.Mark("SplashOverlay-LogoMissing");
        }

        _lottieView = new ImageView(activity);
        _lottieView.SetScaleType(ImageView.ScaleType.FitCenter);
        _lottieView.Elevation = 8f;
        _root.AddView(_lottieView, CenteredLogoParams(activity));
        _lottieView.BringToFront();

        decor.AddView(
            _root,
            new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent));
        _root.BringToFront();

        // Do not wait for Skottie parse / bitmap alloc (multi-second on Android TV).
        // Static logo is enough - BlazorPage must start immediately.
        global::K7.Clients.MAUI.MauiStartupTrace.Mark("SplashOverlay-ReadySignaled");
        SignalReadyToBuildStartPage();

        _driver = new SplashSkottieDriver(activity, _lottieView, LogoWidthDip, LogoHeightDip);
        _driver.FirstOpaqueFrame += OnFirstOpaqueFrame;
        var generation = ++_loadGeneration;
        var appContext = activity.ApplicationContext ?? activity;
        _ = Task.Run(() =>
        {
            SkottieAnimation? animation = null;
            try
            {
                animation = SplashSkottieDriver.LoadAnimation(appContext);
                global::K7.Clients.MAUI.MauiStartupTrace.Mark("SplashOverlay-SkottieParsed");
            }
            catch (Exception)
            {
                animation?.Dispose();
                animation = null;
            }

            if (generation != _loadGeneration)
            {
                animation?.Dispose();
                return;
            }

            var main = new Handler(Looper.MainLooper!);
            // Front of queue so Attach/Start can run before BlazorWebView-initialized freezes it.
            main.PostAtFrontOfQueue(() =>
            {
                if (generation != _loadGeneration || _driver is null || _root is null)
                {
                    animation?.Dispose();
                    return;
                }

                _driver.AttachAnimation(animation);
                if (!_driver.HasAnimation)
                    return;

                global::K7.Clients.MAUI.MauiStartupTrace.Mark("SplashOverlay-SkottieAttached");
                // Always animate while splash is up. Throttle only (do not HideStaticLogo /
                // RenderOneFrame) - that hid the mark and burned ~1s main-thread on TV.
                if (_throttleForBlazor)
                    _driver.SetFrameIntervalMs(SplashSkottieDriver.BlazorBootFrameIntervalMs);

                _driver.Start();
                _root.PostDelayed(HideStaticLogo, StartupLottiePlayback.LogoHideAfterMs);
            });
        });
    }

    public static bool WasReadySignaled => _readySignaled;

    public static void ResumeTicker()
    {
        _driver?.SetFrameIntervalMs(SplashSkottieDriver.FullSpeedFrameIntervalMs);
        _driver?.Resume();
    }

    /// <summary>
    /// ~10fps Skottie while BlazorWebView boots. Keeps the mark visible (unlike a full freeze
    /// + RenderOneFrame that hid the static logo).
    /// </summary>
    public static void PauseTicker()
    {
        _throttleForBlazor = true;
        _driver?.SetFrameIntervalMs(SplashSkottieDriver.BlazorBootFrameIntervalMs);
        global::K7.Clients.MAUI.MauiStartupTrace.Mark("SplashOverlay-TickerThrottled");
    }

    /// <summary>
    /// Called from the WebView JavaBridge thread when index.html runs <c>K7.dismissPreload</c>
    /// (first Blazor frame in the DOM, in-page preload fading). Fades the overlay with it.
    /// </summary>
    public static void NotifyWebContentPainted()
    {
        if (_root is null)
            return;

        global::K7.Clients.MAUI.MauiStartupTrace.Mark("SplashOverlay-web-painted");
        new Handler(Looper.MainLooper!).Post(DismissWithFade);
    }

    /// <summary>
    /// Fades the overlay out over the painted page, then removes it. Idempotent. Main thread.
    /// </summary>
    public static void DismissWithFade()
    {
        var root = _root;
        if (root is null || _fading)
            return;

        _fading = true;
        global::K7.Clients.MAUI.MauiStartupTrace.Mark("splash-fade-start");
        var animator = root.Animate();
        if (animator is null)
        {
            Dismiss();
            return;
        }

        animator.Alpha(0f)!
            .SetDuration(FadeDurationMs)!
            .WithEndAction(new Java.Lang.Runnable(Dismiss))!
            .Start();
    }

    public static void Dismiss()
    {
        var root = _root;
        if (root is null)
            return;

        global::K7.Clients.MAUI.MauiStartupTrace.Mark("splash-dismiss");
        _fading = false;
        _loadGeneration++;
        _throttleForBlazor = false;
        _shownAtMs = 0;

        if (_driver is not null)
            _driver.FirstOpaqueFrame -= OnFirstOpaqueFrame;

        root.RemoveCallbacks(HideStaticLogo);
        root.RemoveCallbacks(SignalReadyToBuildStartPage);

        var driver = _driver;
        _driver = null;
        _lottieView = null;
        _logo = null;
        _root = null;
        ReadyToBuildStartPage = null;

        // Page Loaded and the Skottie attach run inside performTraversals. RemoveView
        // there leaves a null child, and dispatchAttachedToWindow NPEs on Android TV.
        root.Post(() =>
        {
            (root.Parent as ViewGroup)?.RemoveView(root);
            driver?.Teardown();
        });
    }

    private static void OnFirstOpaqueFrame() => HideStaticLogo();

    private static void SignalReadyToBuildStartPage()
    {
        if (_readySignaled)
            return;

        _readySignaled = true;
        ReadyToBuildStartPage?.Invoke();
    }

    private static void HideStaticLogo()
    {
        if (_logoHidden)
            return;

        _logoHidden = true;
        if (_logo is null || _logo.Visibility != ViewStates.Visible)
            return;

        _logo.Visibility = ViewStates.Gone;
    }

    private static FrameLayout.LayoutParams CenteredLogoParams(Context context)
    {
        var metrics = context.Resources!.DisplayMetrics;
        var widthPx = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, LogoWidthDip, metrics);
        var heightPx = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, LogoHeightDip, metrics);
        return new FrameLayout.LayoutParams(widthPx, heightPx)
        {
            Gravity = GravityFlags.Center
        };
    }
}

/// <summary>
/// Skottie driven onto an ImageView. Avoids SKCanvasView PaintSurface, which
/// frequently never fires on Android TV after the SkiaSharp 4 bump.
/// Animation bytes load off the UI thread so TV cold start is not blocked.
/// </summary>
internal sealed class SplashSkottieDriver
{
    public const int FullSpeedFrameIntervalMs = 16;
    public const int BlazorBootFrameIntervalMs = 100;

    private readonly ImageView _view;
    private readonly Handler _ticker = new(Looper.MainLooper!);
    private readonly System.Diagnostics.Stopwatch _watch = new();
    private readonly int _widthPx;
    private readonly int _heightPx;
    private SkottieAnimation? _animation;
    private byte[]? _scratch;
    private int[]? _argbPixels;
    private Java.Lang.IRunnable? _tickRunnable;
    private SKBitmap? _skBitmap;
    private Bitmap? _androidBitmap;
    private bool _running = true;
    private bool _tickerPosted;
    private bool _opaqueRaised;
    private int _frameIntervalMs = FullSpeedFrameIntervalMs;

    public SplashSkottieDriver(Context context, ImageView view, int widthDip, int heightDip)
    {
        _view = view;
        var metrics = context.Resources!.DisplayMetrics;
        _widthPx = Math.Max(1, (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, widthDip, metrics));
        _heightPx = Math.Max(1, (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, heightDip, metrics));
        _tickRunnable = new Java.Lang.Runnable(Tick);
    }

    public event Action? FirstOpaqueFrame;

    public bool HasAnimation => _animation is not null;

    public void AttachAnimation(SkottieAnimation? animation)
    {
        if (animation is null || _animation is not null)
        {
            animation?.Dispose();
            return;
        }

        _animation = animation;

        // Rgba8888 + explicit ARGB pack. Bgra8888 + CopyPixelsFromBuffer swapped R/B
        // on Android TV (cream wordmark rendered blue).
        _skBitmap = new SKBitmap(_widthPx, _heightPx, SKColorType.Rgba8888, SKAlphaType.Premul);
        _androidBitmap = Bitmap.CreateBitmap(_widthPx, _heightPx, Bitmap.Config.Argb8888!);
        _scratch = new byte[_skBitmap.ByteCount];
        _argbPixels = new int[_widthPx * _heightPx];
    }

    public void Start()
    {
        if (!_watch.IsRunning)
            _watch.Start();
        Resume();
    }

    public void Resume()
    {
        if (!_running || _animation is null)
            return;

        _tickerPosted = true;
        if (_tickRunnable is null)
            return;

        _ticker.RemoveCallbacks(_tickRunnable);
        _ticker.Post(_tickRunnable);
    }

    public void Pause()
    {
        _tickerPosted = false;
        if (_tickRunnable is not null)
            _ticker.RemoveCallbacks(_tickRunnable);
    }

    public void SetFrameIntervalMs(int intervalMs)
    {
        _frameIntervalMs = Math.Max(16, intervalMs);
        if (_tickerPosted && _tickRunnable is not null)
        {
            _ticker.RemoveCallbacks(_tickRunnable);
            _ticker.Post(_tickRunnable);
        }
    }

    public void Teardown()
    {
        _running = false;
        _tickerPosted = false;
        if (_tickRunnable is not null)
            _ticker.RemoveCallbacks(_tickRunnable);
        FirstOpaqueFrame = null;
        _androidBitmap?.Recycle();
        _androidBitmap = null;
        _skBitmap?.Dispose();
        _skBitmap = null;
        _animation?.Dispose();
        _animation = null;
    }

    /// <summary>Read + parse splash off the UI thread. Caller owns the returned animation.</summary>
    public static SkottieAnimation? LoadAnimation(Context context)
    {
        try
        {
            using var stream = OpenSplashStream(context);
            if (stream is null)
                return null;

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

    private void Tick()
    {
        if (!_running || !_tickerPosted || _animation is null || _skBitmap is null
            || _androidBitmap is null || _scratch is null || _argbPixels is null)
            return;

        try
        {
            var duration = _animation.Duration;
            var elapsed = _watch.IsRunning ? _watch.Elapsed : TimeSpan.Zero;
            if (duration > TimeSpan.Zero)
            {
                var loopedMs = elapsed.TotalMilliseconds % duration.TotalMilliseconds;
                _animation.Seek(loopedMs / duration.TotalMilliseconds);
            }
            else
            {
                _animation.Seek(0);
            }

            PaintCurrentFrame();
        }
        catch (Exception)
        {
            // Keep ticking; a single bad frame must not kill the splash.
        }

        if (_tickerPosted && _tickRunnable is not null)
            _ticker.PostDelayed(_tickRunnable, _frameIntervalMs);
    }

    private void PaintCurrentFrame()
    {
        if (_animation is null || _skBitmap is null || _androidBitmap is null
            || _scratch is null || _argbPixels is null)
            return;

        using (var canvas = new SKCanvas(_skBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            if (StartupLottiePlayback.TryGetFitRect(
                    _widthPx,
                    _heightPx,
                    _animation.Size.Width,
                    _animation.Size.Height,
                    out var left,
                    out var top,
                    out var right,
                    out var bottom))
            {
                _animation.Render(canvas, new SKRect(left, top, right, bottom));
            }
            else
            {
                _animation.Render(canvas, new SKRect(0, 0, _widthPx, _heightPx));
            }
        }

        CopyToAndroidBitmap(_skBitmap, _androidBitmap, _scratch, _argbPixels);
        _view.SetImageBitmap(_androidBitmap);

        if (!_opaqueRaised && HasOpaquePixels(_skBitmap))
        {
            _opaqueRaised = true;
            FirstOpaqueFrame?.Invoke();
        }
    }

    private static bool HasOpaquePixels(SKBitmap bitmap)
    {
        var span = bitmap.GetPixelSpan();
        // RGBA8888 - alpha every 4th byte starting at offset 3. Sample every 16th pixel.
        for (var i = 3; i < span.Length; i += 64)
        {
            if (span[i] > 8)
                return true;
        }

        return false;
    }

    private static void CopyToAndroidBitmap(
        SKBitmap skia,
        Bitmap android,
        byte[] scratch,
        int[] argbPixels)
    {
        var pixels = skia.GetPixels();
        if (pixels == IntPtr.Zero)
            return;

        System.Runtime.InteropServices.Marshal.Copy(pixels, scratch, 0, scratch.Length);
        for (var i = 0; i < argbPixels.Length; i++)
        {
            var o = i * 4;
            var r = scratch[o];
            var g = scratch[o + 1];
            var b = scratch[o + 2];
            var a = scratch[o + 3];
            argbPixels[i] = (a << 24) | (r << 16) | (g << 8) | b;
        }

        android.SetPixels(argbPixels, 0, skia.Width, 0, 0, skia.Width, skia.Height);
    }

    private static Stream? OpenSplashStream(Context context)
    {
        try
        {
            var asset = context.Assets?.Open("splash.json");
            if (asset is not null)
                return asset;
        }
        catch (Java.IO.IOException)
        {
        }

        try
        {
            return FileSystem.OpenAppPackageFileAsync("splash.json").GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            return null;
        }
    }
}

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

    public static bool IsShown => _root is not null;

    public static bool HasAnimation => _driver?.HasAnimation == true;

    public static event Action? ReadyToBuildStartPage;

    public static void Show(AActivity activity)
    {
        if (_root is not null || activity.Window?.DecorView is not ViewGroup decor)
            return;

        _readySignaled = false;
        _logoHidden = false;
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
        }

        _lottieView = new ImageView(activity);
        _lottieView.SetScaleType(ImageView.ScaleType.FitCenter);
        _lottieView.Elevation = 8f;
        _root.AddView(_lottieView, CenteredLogoParams(activity));
        _lottieView.BringToFront();

        _driver = new SplashSkottieDriver(activity, _lottieView, LogoWidthDip, LogoHeightDip);
        _driver.FirstOpaqueFrame += OnFirstOpaqueFrame;

        decor.AddView(
            _root,
            new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent));
        _root.BringToFront();

        _driver.Start();

        // Blazor under the looping mark right away. Skottie blit still janks during
        // MediaElement init, but delaying only lengthens cold start.
        if (_driver.HasAnimation)
        {
            SignalReadyToBuildStartPage();
            _root.PostDelayed(HideStaticLogo, StartupLottiePlayback.LogoHideAfterMs);
        }
        else
        {
            _root.PostDelayed(SignalReadyToBuildStartPage, StartupLottiePlayback.NoAnimationReadyMs);
        }
    }

    public static void ResumeTicker() => _driver?.Resume();

    public static void Dismiss()
    {
        if (_root is null)
            return;

        if (_driver is not null)
            _driver.FirstOpaqueFrame -= OnFirstOpaqueFrame;

        _root.RemoveCallbacks(HideStaticLogo);
        _root.RemoveCallbacks(SignalReadyToBuildStartPage);
        (_root.Parent as ViewGroup)?.RemoveView(_root);
        _driver?.Teardown();
        _driver = null;
        _lottieView = null;
        _logo = null;
        _root = null;
        ReadyToBuildStartPage = null;
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
/// </summary>
internal sealed class SplashSkottieDriver
{
    private readonly ImageView _view;
    private readonly SkottieAnimation? _animation;
    private readonly Handler _ticker = new(Looper.MainLooper!);
    private readonly System.Diagnostics.Stopwatch _watch = new();
    private readonly int _widthPx;
    private readonly int _heightPx;
    private readonly byte[]? _scratch;
    private readonly int[]? _argbPixels;
    private Java.Lang.IRunnable? _tickRunnable;
    private SKBitmap? _skBitmap;
    private Bitmap? _androidBitmap;
    private bool _running = true;
    private bool _tickerPosted;
    private bool _opaqueRaised;

    public SplashSkottieDriver(Context context, ImageView view, int widthDip, int heightDip)
    {
        _view = view;
        var metrics = context.Resources!.DisplayMetrics;
        _widthPx = Math.Max(1, (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, widthDip, metrics));
        _heightPx = Math.Max(1, (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, heightDip, metrics));
        _animation = LoadAnimation(context);
        if (_animation is not null)
        {
            // Rgba8888 + explicit ARGB pack. Bgra8888 + CopyPixelsFromBuffer swapped R/B
            // on Android TV (cream wordmark rendered blue).
            _skBitmap = new SKBitmap(_widthPx, _heightPx, SKColorType.Rgba8888, SKAlphaType.Premul);
            _androidBitmap = Bitmap.CreateBitmap(_widthPx, _heightPx, Bitmap.Config.Argb8888!);
            _scratch = new byte[_skBitmap.ByteCount];
            _argbPixels = new int[_widthPx * _heightPx];
        }

        _tickRunnable = new Java.Lang.Runnable(Tick);
    }

    public event Action? FirstOpaqueFrame;

    public bool HasAnimation => _animation is not null;

    public long DurationMs => _animation is null ? 0 : (long)_animation.Duration.TotalMilliseconds;

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
        catch (Exception)
        {
            // Keep ticking; a single bad frame must not kill the splash.
        }

        if (_tickRunnable is not null)
            _ticker.PostDelayed(_tickRunnable, 16);
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

    private static SkottieAnimation? LoadAnimation(Context context)
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

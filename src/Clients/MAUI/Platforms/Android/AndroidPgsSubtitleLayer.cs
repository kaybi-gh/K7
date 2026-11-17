using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Media3.UI;
using K7.Clients.MAUI.Controls.Video;
using K7.Clients.Shared.Helpers;
using Paint = Android.Graphics.Paint;
using RectF = Android.Graphics.RectF;
using View = Android.Views.View;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// Draws embedded PGS (Blu-ray bitmap) subtitles over ExoPlayer from the server .sup
/// sidecar, so an HLS stream keeps its video copy instead of a burn-in transcode. The
/// bitmap view lives in the PlayerView overlay frame, next to the text cues, and is GONE
/// whenever the plane is empty so it never blends over the video surface for nothing.
/// </summary>
internal sealed class AndroidPgsSubtitleLayer(Func<PlayerView?> playerView, Func<double> positionSeconds)
{
    private const long TickMilliseconds = 100;

    private readonly Handler _handler = new(Looper.MainLooper!);
    private PgsSubtitleLoader? _loader;
    private (Guid FileId, int TrackIndex)? _key;
    private PgsOverlayView? _view;
    private PgsFrame? _shown;
    private int _generation;

    public void Show(Guid fileId, int trackIndex, PgsWindowSource source)
    {
        if (_loader is not null && _key == (fileId, trackIndex))
            return;

        Stop();
        _key = (fileId, trackIndex);
        _loader = new PgsSubtitleLoader(source);
        NativeVideoDebug.Log("PgsLayer start track=" + trackIndex);
        Tick(_generation);
    }

    public void Stop()
    {
        _generation++;
        _loader?.Dispose();
        _loader = null;
        _key = null;
        _shown = null;

        var view = _view;
        _view = null;
        if (view is null)
            return;

        try
        {
            view.SetFrame(null);
            (view.Parent as ViewGroup)?.RemoveView(view);
        }
        catch
        {
        }
    }

    private void Tick(int generation)
    {
        var loader = _loader;
        if (generation != _generation || loader is null)
            return;

        try
        {
            // ExoPlayer reports 0 until the source is prepared: do not load a window at
            // the start of the file for a resume that is about to land elsewhere.
            var position = positionSeconds();
            if (position > 0)
            {
                loader.Update(position);
                Render(loader.FrameAt(position));
            }
        }
        catch
        {
        }

        _handler.PostDelayed(() => Tick(generation), TickMilliseconds);
    }

    private void Render(PgsFrame? frame)
    {
        var host = playerView();
        var overlay = host?.OverlayFrameLayout;
        if (host is null || overlay is null || overlay.Context is null)
            return;

        if (_view is null || !ReferenceEquals(_view.Parent, overlay))
        {
            if (_view?.Parent is ViewGroup previous)
                previous.RemoveView(_view);

            _view = new PgsOverlayView(overlay.Context);
            overlay.AddView(
                _view,
                new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            _shown = null;
        }

        _view.SetPicture(ResolvePictureBounds(host, overlay));
        if (ReferenceEquals(frame, _shown))
            return;

        _shown = frame;
        _view.SetFrame(frame);
    }

    /// <summary>
    /// Where the picture sits inside the overlay: the video surface is sized to the video
    /// aspect by the PlayerView content frame, for both fit and zoom.
    /// </summary>
    private static RectF ResolvePictureBounds(PlayerView host, View overlay)
    {
        var surface = host.VideoSurfaceView;
        if (surface is null || surface.Width <= 0 || surface.Height <= 0)
            return new RectF(0, 0, overlay.Width, overlay.Height);

        var surfaceOrigin = new int[2];
        var overlayOrigin = new int[2];
        surface.GetLocationInWindow(surfaceOrigin);
        overlay.GetLocationInWindow(overlayOrigin);
        var left = surfaceOrigin[0] - overlayOrigin[0];
        var top = surfaceOrigin[1] - overlayOrigin[1];
        return new RectF(left, top, left + surface.Width, top + surface.Height);
    }

    private sealed class PgsOverlayView : View
    {
        private readonly List<(Bitmap Bitmap, int X, int Y)> _bitmaps = [];
        private readonly Paint _paint = new(PaintFlags.FilterBitmap | PaintFlags.AntiAlias);
        private readonly RectF _target = new();
        private RectF _picture = new();
        private int _planeWidth;
        private int _planeHeight;

        public PgsOverlayView(Context context)
            : base(context)
        {
            SetWillNotDraw(false);
            Focusable = false;
            Clickable = false;
            Visibility = ViewStates.Gone;
        }

        private PgsOverlayView(IntPtr handle, JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }

        public void SetPicture(RectF picture)
        {
            if (picture.Left == _picture.Left
                && picture.Top == _picture.Top
                && picture.Right == _picture.Right
                && picture.Bottom == _picture.Bottom)
            {
                return;
            }

            _picture = picture;
            Invalidate();
        }

        public void SetFrame(PgsFrame? frame)
        {
            foreach (var (bitmap, _, _) in _bitmaps)
                bitmap.Recycle();
            _bitmaps.Clear();

            if (frame is not null)
            {
                _planeWidth = frame.PlaneWidth;
                _planeHeight = frame.PlaneHeight;
                foreach (var pgsObject in frame.Objects)
                {
                    var bitmap = ToBitmap(pgsObject);
                    if (bitmap is not null)
                        _bitmaps.Add((bitmap, pgsObject.X, pgsObject.Y));
                }
            }

            Visibility = _bitmaps.Count > 0 ? ViewStates.Visible : ViewStates.Gone;
            Invalidate();
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            if (_bitmaps.Count == 0 || _planeWidth <= 0 || _planeHeight <= 0 || Width <= 0 || Height <= 0)
                return;

            var pictureWidth = _picture.Width() > 0 ? _picture.Width() : Width;
            var pictureHeight = _picture.Height() > 0 ? _picture.Height() : Height;
            var centerX = _picture.Width() > 0 ? _picture.CenterX() : Width / 2f;
            var centerY = _picture.Height() > 0 ? _picture.CenterY() : Height / 2f;

            // The subtitle plane (often 16:9) matches the picture along its uncropped side,
            // so lines authored in a letterbox stay under a cinema-scope picture.
            var scale = Math.Max(pictureWidth / _planeWidth, pictureHeight / _planeHeight);
            var planeLeft = centerX - (_planeWidth * scale / 2f);
            var planeTop = centerY - (_planeHeight * scale / 2f);

            foreach (var (bitmap, x, y) in _bitmaps)
            {
                var width = bitmap.Width * scale;
                var height = bitmap.Height * scale;
                // Keep the object inside the player (zoom crop, or a short player).
                var shrink = Math.Min(1f, Math.Min(Width / width, Height / height));
                width *= shrink;
                height *= shrink;
                var left = Math.Clamp(planeLeft + (x * scale), 0f, Width - width);
                var top = Math.Clamp(planeTop + (y * scale), 0f, Height - height);
                _target.Set(left, top, left + width, top + height);
                canvas.DrawBitmap(bitmap, null, _target, _paint);
            }
        }

        private static Bitmap? ToBitmap(PgsFrameObject pgsObject)
        {
            var count = pgsObject.Width * pgsObject.Height;
            if (count <= 0 || pgsObject.Rgba.Length < count * 4)
                return null;

            // createBitmap(int[]) takes straight (not premultiplied) ARGB.
            var colors = new int[count];
            var rgba = pgsObject.Rgba;
            for (var i = 0; i < count; i++)
            {
                var offset = i * 4;
                colors[i] = (rgba[offset + 3] << 24)
                    | (rgba[offset] << 16)
                    | (rgba[offset + 1] << 8)
                    | rgba[offset + 2];
            }

            return Bitmap.CreateBitmap(colors, pgsObject.Width, pgsObject.Height, Bitmap.Config.Argb8888!);
        }
    }
}

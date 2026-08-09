using System.Globalization;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Native seek bar: progress, buffered, chapter ticks, pointer scrub, TV step scrub.
/// </summary>
public sealed class NativeSeekBar : GraphicsView
{
    public static readonly BindableProperty PlayerProperty =
        BindableProperty.Create(nameof(Player), typeof(IPlayerService), typeof(NativeSeekBar));

    public static readonly BindableProperty ChaptersProperty =
        BindableProperty.Create(
            nameof(Chapters),
            typeof(IReadOnlyList<SeekBarChapterBuilder.Marker>),
            typeof(NativeSeekBar),
            defaultValue: Array.Empty<SeekBarChapterBuilder.Marker>(),
            propertyChanged: (b, _, _) => ((NativeSeekBar)b).Invalidate());

    public static readonly BindableProperty PreviewTimeProperty =
        BindableProperty.Create(
            nameof(PreviewTime),
            typeof(double?),
            typeof(NativeSeekBar),
            propertyChanged: (b, _, _) => ((NativeSeekBar)b).Invalidate());

    public event EventHandler<bool>? DragChanged;
    public event EventHandler<double>? SeekCommitted;

    private bool _dragging;
    private double? _dragTime;
    private readonly SeekBarDrawable _drawable;

    public NativeSeekBar()
    {
        HeightRequest = 28;
        _drawable = new SeekBarDrawable(this);
        Drawable = _drawable;
        StartInteraction += OnStartInteraction;
        DragInteraction += OnDragInteraction;
        EndInteraction += OnEndInteraction;
    }

    public IPlayerService? Player
    {
        get => (IPlayerService?)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }

    public IReadOnlyList<SeekBarChapterBuilder.Marker> Chapters
    {
        get => (IReadOnlyList<SeekBarChapterBuilder.Marker>)GetValue(ChaptersProperty);
        set => SetValue(ChaptersProperty, value);
    }

    public double? PreviewTime
    {
        get => (double?)GetValue(PreviewTimeProperty);
        set => SetValue(PreviewTimeProperty, value);
    }

    public bool IsDragging => _dragging;

    public double DisplayTime
    {
        get
        {
            if (_dragTime is double drag)
                return drag;
            if (PreviewTime is double preview)
                return preview;
            return Player?.CurrentTime ?? 0;
        }
    }

    public void Refresh() => Invalidate();

    public void BeginEdit()
    {
        if (_dragging)
            return;
        _dragging = true;
        _dragTime = Player?.CurrentTime ?? 0;
        DragChanged?.Invoke(this, true);
        Refresh();
    }

    public void ScrubBy(double deltaSeconds)
    {
        if (!_dragging)
            BeginEdit();

        var duration = Math.Max(Player?.Duration ?? 0, 0.001);
        var next = Math.Clamp((_dragTime ?? Player?.CurrentTime ?? 0) + deltaSeconds, 0, duration);
        _dragTime = next;
        Refresh();
    }

    public void CommitEdit()
    {
        if (!_dragging)
            return;

        var time = _dragTime ?? Player?.CurrentTime ?? 0;
        _dragging = false;
        _dragTime = null;
        Player?.Seek(time);
        SeekCommitted?.Invoke(this, time);
        DragChanged?.Invoke(this, false);
        Refresh();
    }

    public void CancelEdit()
    {
        if (!_dragging)
            return;

        _dragging = false;
        _dragTime = null;
        DragChanged?.Invoke(this, false);
        Refresh();
    }

    private void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0 || Player is null || Player.Duration <= 0)
            return;

        _dragging = true;
        _dragTime = TimeFromX(e.Touches[0].X);
        DragChanged?.Invoke(this, true);
        Refresh();
    }

    private void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        if (!_dragging || e.Touches.Length == 0)
            return;

        _dragTime = TimeFromX(e.Touches[0].X);
        Refresh();
    }

    private void OnEndInteraction(object? sender, TouchEventArgs e)
    {
        if (!_dragging)
            return;

        CommitEdit();
    }

    private double TimeFromX(float x)
    {
        var duration = Math.Max(Player?.Duration ?? 0, 0.001);
        var ratio = Math.Clamp(x / Math.Max(Width, 1), 0, 1);
        return ratio * duration;
    }

    private sealed class SeekBarDrawable(NativeSeekBar owner) : IDrawable
    {
        private const float ChapterGapPx = 3f;
        private const float TrackHeight = 6f;
        private const float TrackHeightFocused = 8f;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var player = owner.Player;
            var duration = player?.Duration ?? 0;
            if (duration <= 0 || dirtyRect.Width <= 0)
                return;

            var trackY = dirtyRect.Height / 2f;
            var trackHeight = owner._dragging ? TrackHeightFocused : TrackHeight;
            var currentTime = owner.DisplayTime;
            var bufferedTime = player?.BufferedTime ?? 0;
            var chapters = owner.Chapters;

            if (chapters.Count == 0)
            {
                DrawSolidTrack(canvas, dirtyRect, trackY, trackHeight, duration, currentTime, bufferedTime);
            }
            else
            {
                DrawChapterTubes(
                    canvas, dirtyRect, trackY, trackHeight, duration, currentTime, bufferedTime, chapters);
            }

            var current = Math.Clamp(currentTime / duration, 0, 1);
            var thumbX = dirtyRect.Width * (float)current;
            canvas.FillColor = Colors.White;
            canvas.FillCircle(thumbX, trackY, owner._dragging ? 8 : 6);
        }

        private static void DrawSolidTrack(
            ICanvas canvas,
            RectF dirtyRect,
            float trackY,
            float trackHeight,
            double duration,
            double currentTime,
            double bufferedTime)
        {
            var trackRect = new RectF(0, trackY - trackHeight / 2f, dirtyRect.Width, trackHeight);
            canvas.FillColor = Color.FromArgb("#4DFFFFFF");
            canvas.FillRoundedRectangle(trackRect, 4);

            var buffered = Math.Clamp(bufferedTime / duration, 0, 1);
            canvas.FillColor = Color.FromArgb("#33FFFFFF");
            canvas.FillRoundedRectangle(
                new RectF(0, trackY - trackHeight / 2f, dirtyRect.Width * (float)buffered, trackHeight),
                4);

            var current = Math.Clamp(currentTime / duration, 0, 1);
            canvas.FillColor = Color.FromArgb("#CCE50914");
            canvas.FillRoundedRectangle(
                new RectF(0, trackY - trackHeight / 2f, dirtyRect.Width * (float)current, trackHeight),
                4);
        }

        private static void DrawChapterTubes(
            ICanvas canvas,
            RectF dirtyRect,
            float trackY,
            float trackHeight,
            double duration,
            double currentTime,
            double bufferedTime,
            IReadOnlyList<SeekBarChapterBuilder.Marker> chapters)
        {
            var segments = new List<(double Start, double End)>(chapters.Count);
            for (var i = 0; i < chapters.Count; i++)
            {
                var start = chapters[i].StartSeconds;
                var end = i + 1 < chapters.Count ? chapters[i + 1].StartSeconds : duration;
                if (end <= start)
                    continue;
                segments.Add((start, end));
            }

            if (segments.Count == 0)
            {
                DrawSolidTrack(canvas, dirtyRect, trackY, trackHeight, duration, currentTime, bufferedTime);
                return;
            }

            var gapsTotal = Math.Max(0, segments.Count - 1) * ChapterGapPx;
            var usableWidth = Math.Max(0, dirtyRect.Width - gapsTotal);

            for (var i = 0; i < segments.Count; i++)
            {
                var (start, end) = segments[i];
                var startFrac = start / duration;
                var durFrac = (end - start) / duration;
                var left = (float)(startFrac * usableWidth) + i * ChapterGapPx;
                var width = Math.Max(2f, (float)(durFrac * usableWidth));
                var tube = new RectF(left, trackY - trackHeight / 2f, width, trackHeight);

                canvas.FillColor = Color.FromArgb("#4DFFFFFF");
                canvas.FillRoundedRectangle(tube, 4);

                // Buffered relative to this segment.
                var bufferedInSeg = Math.Clamp((bufferedTime - start) / (end - start), 0, 1);
                if (bufferedInSeg > 0)
                {
                    canvas.FillColor = Color.FromArgb("#33FFFFFF");
                    canvas.FillRoundedRectangle(
                        new RectF(left, tube.Y, width * (float)bufferedInSeg, trackHeight),
                        4);
                }

                var progressInSeg = Math.Clamp((currentTime - start) / (end - start), 0, 1);
                if (progressInSeg > 0)
                {
                    canvas.FillColor = Color.FromArgb("#CCE50914");
                    canvas.FillRoundedRectangle(
                        new RectF(left, tube.Y, width * (float)progressInSeg, trackHeight),
                        4);
                }
            }
        }
    }
}

public static class NativeTimeFormatting
{
    public static string Format(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            seconds = 0;

        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : ts.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}

using K7.Clients.Shared.Helpers;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Vertical volume pill matching the web overlay slider (tall track, fill from the bottom).
/// WinUI <see cref="Slider"/> stays horizontal even when taller than wide.
/// </summary>
public sealed class NativeVolumeSlider : GraphicsView
{
    public event EventHandler<double>? ValueChanged;

    private double _value = 1;
    private bool _dragging;

    public NativeVolumeSlider()
    {
        WidthRequest = 48;
        HeightRequest = 200;
        Drawable = new VolumeDrawable(this);

        if (NativePointerInput.SupportsHoverRecognizers)
        {
            var pointer = new PointerGestureRecognizer();
            pointer.PointerPressed += (_, e) => ApplyFromPointer(e, dragging: true);
            pointer.PointerMoved += (_, e) =>
            {
                if (_dragging)
                    ApplyFromPointer(e, dragging: true);
            };
            pointer.PointerReleased += (_, _) => _dragging = false;
            pointer.PointerExited += (_, _) => _dragging = false;
            GestureRecognizers.Add(pointer);
        }

        StartInteraction += (_, e) =>
        {
            if (e.Touches.Length == 0)
                return;
            ApplyFromY(e.Touches[0].Y, dragging: true);
        };
        DragInteraction += (_, e) =>
        {
            if (!_dragging || e.Touches.Length == 0)
                return;
            ApplyFromY(e.Touches[0].Y, dragging: true);
        };
        EndInteraction += (_, _) => _dragging = false;
    }

    public bool IsDragging => _dragging;

    public double Value
    {
        get => _value;
        set
        {
            var next = Math.Clamp(value, 0, 1);
            if (Math.Abs(_value - next) < 0.001)
                return;

            _value = next;
            Invalidate();
        }
    }

    private void ApplyFromPointer(PointerEventArgs e, bool dragging)
    {
        var point = e.GetPosition(this);
        if (point is null)
            return;

        ApplyFromY(point.Value.Y, dragging);
    }

    private void ApplyFromY(double y, bool dragging)
    {
        _dragging = dragging;
        var height = Math.Max(Height, 1);
        var next = Math.Clamp(1 - (y / height), 0, 1);
        if (Math.Abs(_value - next) < 0.001)
            return;

        _value = next;
        Invalidate();
        ValueChanged?.Invoke(this, _value);
    }

    private sealed class VolumeDrawable(NativeVolumeSlider owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var padX = 16f;
            var track = new RectF(
                padX,
                8,
                Math.Max(8, dirtyRect.Width - padX * 2),
                Math.Max(16, dirtyRect.Height - 16));

            canvas.FillColor = Color.FromArgb("#33FFFFFF");
            canvas.FillRoundedRectangle(track, track.Width / 2);

            var fillHeight = track.Height * (float)owner._value;
            if (fillHeight <= 0)
                return;

            var fill = new RectF(
                track.X,
                track.Bottom - fillHeight,
                track.Width,
                fillHeight);
            canvas.FillColor = Colors.White;
            canvas.FillRoundedRectangle(fill, track.Width / 2);
        }
    }
}

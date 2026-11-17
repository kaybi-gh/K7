namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Loaded PGS screens ordered by time. A screen stays up until the next one replaces it,
/// but never past what has loaded: beyond <see cref="LoadedUntilSeconds"/> the clear that
/// ends it may simply not have arrived yet.
/// </summary>
public sealed class PgsTimeline
{
    /// <summary>Screens further behind the playhead than this are dropped.</summary>
    public const double KeepBehindSeconds = 120;

    private readonly List<PgsFrame> _frames = [];

    public double LoadedUntilSeconds { get; private set; }

    public int Count => _frames.Count;

    public void Reset()
    {
        _frames.Clear();
        LoadedUntilSeconds = 0;
    }

    public void Add(PgsFrame frame)
    {
        var index = _frames.Count;
        while (index > 0 && _frames[index - 1].TimeSeconds > frame.TimeSeconds)
            index--;

        _frames.Insert(index, frame);
        MarkLoaded(frame.TimeSeconds);
    }

    public void MarkLoaded(double seconds)
    {
        if (seconds > LoadedUntilSeconds)
            LoadedUntilSeconds = seconds;
    }

    /// <summary>
    /// The screen to draw at <paramref name="seconds"/>, or null when the plane is empty.
    /// </summary>
    public PgsFrame? FrameAt(double seconds)
    {
        if (seconds > LoadedUntilSeconds)
            return null;

        var low = 0;
        var high = _frames.Count - 1;
        PgsFrame? found = null;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (_frames[middle].TimeSeconds <= seconds)
            {
                found = _frames[middle];
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found is { IsClear: false } ? found : null;
    }

    /// <summary>
    /// Drops screens that ended long before <paramref name="seconds"/>. The one still on
    /// screen is kept whatever its age.
    /// </summary>
    public void Prune(double seconds)
    {
        var threshold = seconds - KeepBehindSeconds;
        var remove = 0;
        while (remove + 1 < _frames.Count && _frames[remove + 1].TimeSeconds <= threshold)
            remove++;

        if (remove > 0)
            _frames.RemoveRange(0, remove);
    }
}

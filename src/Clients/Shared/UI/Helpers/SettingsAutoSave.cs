using K7.Clients.Shared.Helpers;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Debounced save-on-change for settings pages without a save bar. The page calls
/// <see cref="Schedule"/> after each render with the snapshot of its dirty form; the save runs
/// once the form has been quiet for the delay.
/// </summary>
public sealed class SettingsAutoSave(int delayMs = 700) : IDisposable
{
    private CancellationTokenSource? _cts;
    /// <summary>
    /// Snapshot the pending (or last failed) save was scheduled for. A render that does not
    /// change the form must not restart the timer, and a failed save must not retry in a loop.
    /// </summary>
    private string? _snapshot;

    /// <summary>Schedules <paramref name="save"/> unless it is already pending for this snapshot.</summary>
    public void Schedule(string snapshot, Func<Task> save)
    {
        if (snapshot == _snapshot)
            return;

        _snapshot = snapshot;
        Cancel();
        _cts = new CancellationTokenSource();
        RunAsync(save, _cts.Token).FireAndForget();
    }

    /// <summary>Drops the pending save, keeping the snapshot so the same state is not rescheduled.</summary>
    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>Forgets the snapshot after a successful save or a reset, so later edits schedule again.</summary>
    public void Reset()
    {
        Cancel();
        _snapshot = null;
    }

    public void Dispose() => Cancel();

    private async Task RunAsync(Func<Task> save, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delayMs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await save();
    }
}

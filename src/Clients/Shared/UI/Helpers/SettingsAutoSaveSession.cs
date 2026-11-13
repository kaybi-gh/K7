using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.UI.Components;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Save-on-change for a personal settings page: no save bar, the form is sent once it has been
/// quiet for the delay. The page calls <see cref="OnAfterRender"/> from its own
/// <c>OnAfterRender</c>, shows <see cref="State"/> in a <c>K7SaveIndicator</c> and disposes the
/// session with itself, which sends a change still waiting for the delay.
/// </summary>
/// <param name="tracker">Tracker holding the last saved state of the form.</param>
/// <param name="getState">Current form state, or null while the page is not ready.</param>
/// <param name="persist">Sends the current form and refreshes whatever depends on it.</param>
/// <param name="dispatch">Runs work on the page's renderer, usually <c>InvokeAsync</c>.</param>
/// <param name="render">Asks the page to render again, usually <c>StateHasChanged</c>.</param>
/// <param name="onError">Reports a failed save or operation to the user.</param>
/// <param name="delayMs">Quiet time before a change is sent.</param>
public sealed class SettingsAutoSaveSession<T>(
    SettingsFormTracker<T> tracker,
    Func<T?> getState,
    Func<Task> persist,
    Func<Func<Task>, Task> dispatch,
    Action render,
    Action<Exception> onError,
    int delayMs = 700) : IDisposable where T : class
{
    private readonly SettingsAutoSave _autoSave = new(delayMs);
    private bool _disposed;

    /// <summary>True while a save or a <see cref="RunAsync"/> operation is in flight.</summary>
    public bool IsBusy { get; private set; }

    public bool IsDirty => getState() is { } state && tracker.IsDirty(state);

    /// <summary>
    /// Edits waiting for the delay already count as saving, so the indicator stays visible from
    /// the change to the end of the request instead of flashing.
    /// </summary>
    public SettingsSaveState State
    {
        get => field != SettingsSaveState.Failed && IsDirty ? SettingsSaveState.Saving : field;
        private set;
    }

    /// <summary>Schedules a save when the render left the form dirty.</summary>
    public void OnAfterRender()
    {
        if (IsBusy || _disposed || getState() is not { } state || !tracker.IsDirty(state))
            return;

        _autoSave.Schedule(tracker.Snapshot(state), () => dispatch(SaveAsync));
    }

    /// <summary>Sends the form now, without waiting for the delay.</summary>
    public async Task SaveAsync()
    {
        if (IsBusy || getState() is not { } state)
            return;

        _autoSave.Cancel();
        IsBusy = true;
        State = SettingsSaveState.Saving;
        render();
        try
        {
            // Snapshot what is sent: edits made while the request is in flight stay dirty
            // and trigger the next save.
            var sentSnapshot = tracker.Snapshot(state);
            await persist();
            tracker.CaptureSnapshot(sentSnapshot);
            _autoSave.Reset();
            State = SettingsSaveState.Saved;
        }
        catch (Exception ex)
        {
            State = SettingsSaveState.Failed;
            onError(ex);
        }
        finally
        {
            IsBusy = false;
            if (!_disposed)
                render();
        }
    }

    /// <summary>
    /// Runs an operation that replaces the form (reset to defaults), dropping any pending save.
    /// The operation captures the new state in the tracker itself.
    /// </summary>
    public async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy)
            return;

        _autoSave.Reset();
        State = SettingsSaveState.Idle;
        IsBusy = true;
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            onError(ex);
        }
        finally
        {
            IsBusy = false;
            if (!_disposed)
                render();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _autoSave.Dispose();

        // Leaving inside the quiet time: send the last change instead of dropping it.
        if (!IsBusy && IsDirty)
            persist().FireAndForget();
    }
}

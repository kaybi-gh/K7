using K7.Clients.Shared.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Helpers;

public sealed class SelectionModeKeyboardBinder : IAsyncDisposable
{
    private readonly ISpatialNavService _spatialNav;
    private readonly Action _onEscape;
    private readonly Action _onSelectAll;
    private DotNetObjectReference<SelectionModeKeyboardCallback>? _ref;
    private bool _enabled;

    public SelectionModeKeyboardBinder(ISpatialNavService spatialNav, Action onEscape, Action onSelectAll)
    {
        _spatialNav = spatialNav;
        _onEscape = onEscape;
        _onSelectAll = onSelectAll;
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        if (enabled == _enabled)
            return;

        _enabled = enabled;
        if (enabled)
        {
            _ref ??= DotNetObjectReference.Create(new SelectionModeKeyboardCallback(_onEscape, _onSelectAll));
            await _spatialNav.RegisterSelectionModeAsync(_ref);
            return;
        }

        await _spatialNav.UnregisterSelectionModeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_enabled)
        {
            _enabled = false;
            await _spatialNav.UnregisterSelectionModeAsync();
        }

        _ref?.Dispose();
        _ref = null;
    }
}

public sealed class SelectionModeKeyboardCallback(Action onEscape, Action onSelectAll)
{
    [JSInvokable]
    public void OnSelectionEscape() => onEscape();

    [JSInvokable]
    public void OnSelectionSelectAll() => onSelectAll();
}

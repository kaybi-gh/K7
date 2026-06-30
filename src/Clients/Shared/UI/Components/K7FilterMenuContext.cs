namespace K7.Clients.Shared.UI.Components;

public sealed class K7FilterMenuContext
{
    private Action? _stateChanged;

    public string? ActiveSubmenu { get; private set; }

    public void Register(Action stateChanged) => _stateChanged = stateChanged;

    public void Navigate(string? submenu)
    {
        ActiveSubmenu = submenu;
        _stateChanged?.Invoke();
    }

    public void NavigateBack()
    {
        ActiveSubmenu = null;
        _stateChanged?.Invoke();
    }

    public void Reset()
    {
        ActiveSubmenu = null;
        _stateChanged?.Invoke();
    }
}

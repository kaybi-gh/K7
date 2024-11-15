namespace MediaClient.Shared.Services;

public class SidebarService
{
    public event Action? IsOpenOnChange;

    private bool? _isOpen;
    public bool IsOpen
    {
        get => _isOpen ?? false;
        set
        {
            if (_isOpen != value)
            {
                _isOpen = value;
                IsOpenOnChange?.Invoke();
            }
        }
    }
    
    public void ToggleIsOpen()
    {
        IsOpen = !IsOpen;
    }
}
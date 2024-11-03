namespace MediaClient.Shared.Services;

public class PlayerService
{
    public event Action? PosterOnChange;

    private string? _poster;
    public string? Poster
    {
        get => _poster ?? "";
        set
        {
            if (_poster != value)
            {
                _poster = value;
                PosterOnChange?.Invoke();
            }
        }
    }

    public event Action? SourcesOnChange;

    private List<string>? _sources;
    public List<string> Sources
    {
        get => _sources ?? new();
        set
        {
            if (_sources != value)
            {
                _sources = value;
                SourcesOnChange?.Invoke();
            }
        }
    }

    public event Action? IsVisibleOnChange;

    private bool? _isVisible;
    public bool IsVisible
    {
        get => _isVisible ?? false;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                IsVisibleOnChange?.Invoke();
            }
        }
    }
    
    public void ToggleIsVisible()
    {
        IsVisible = !IsVisible;
    }

    public void Show()
    {
        IsVisible = true;
    }

    public void Hide()
    {
        IsVisible = false;
    }
}
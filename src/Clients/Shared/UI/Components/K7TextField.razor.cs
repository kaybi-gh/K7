using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7TextField<TValue> : IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string Placeholder { get; set; } = "";
    [Parameter] public string Type { get; set; } = "text";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public string RequiredError { get; set; } = "";
    [Parameter] public int MaxLength { get; set; }
    [Parameter] public int Lines { get; set; } = 1;
    [Parameter] public bool Immediate { get; set; }
    [Parameter] public string Variant { get; set; } = "outlined";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Adornment { get; set; } = "";
    [Parameter] public string AdornmentIcon { get; set; } = "";
    [Parameter] public EventCallback OnAdornmentClick { get; set; }
    [Parameter] public bool PasswordToggle { get; set; } = true;
    [Parameter] public int DebounceInterval { get; set; }
    [Parameter] public EventCallback<TValue?> OnDebounceIntervalElapsed { get; set; }
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public string HelperText { get; set; } = "";
    [Parameter] public bool Clearable { get; set; }
    [Parameter] public Func<TValue?, string?>? Validation { get; set; }
    [Parameter] public EventCallback<FocusEventArgs> OnFocus { get; set; }
    [Parameter] public EventCallback<FocusEventArgs> OnFocusOut { get; set; }
    [Parameter] public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }
    [Parameter] public EventCallback<KeyboardEventArgs> OnKeyUp { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
    [Parameter] public bool DisableSpatialActivatable { get; set; }
    [Parameter] public bool ForceSpatialActivatable { get; set; }
    [Parameter] public bool Autofocus { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? UserAttributes { get; set; }

    private readonly string _id = $"k7tf-{Guid.NewGuid():N}";
    private ElementReference _inputRef;
    private bool _hasError;
    private string _errorText = "";
    private Timer? _debounceTimer;
    private bool _disposed;
    private bool _passwordRevealed;

    private bool IsPasswordField =>
        string.Equals(Type, "password", StringComparison.OrdinalIgnoreCase);

    private bool ShowPasswordToggleButton => IsPasswordField && PasswordToggle && Lines <= 1;

    private string EffectiveInputType =>
        ShowPasswordToggleButton && _passwordRevealed ? "text" : Type;

    private bool HasAdornment =>
        ShowPasswordToggleButton
        || (Clearable && Value is not null && !string.IsNullOrEmpty(Value.ToString()))
        || !string.IsNullOrEmpty(AdornmentIcon);

    private string? SpatialActivatable =>
        !Disabled && (!ReadOnly || ForceSpatialActivatable) && !DisableSpatialActivatable ? "" : null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Autofocus && !Disabled)
            await EnsureEditModeAsync();
    }

    private async Task HandleFocusAsync(FocusEventArgs e)
    {
        // Activatable inputs can be refocused by spatial-nav without edit mode
        // (readonly). Autofocus means the user should always be able to type.
        if (Autofocus && !Disabled)
            await EnsureEditModeAsync();

        if (OnFocus.HasDelegate)
            await OnFocus.InvokeAsync(e);
    }

    private async Task EnsureEditModeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("SpatialNav.startEditing", _inputRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            try { await _inputRef.FocusAsync(); }
            catch (InvalidOperationException) { }
        }
    }

    private async Task OnInput(ChangeEventArgs e)
    {
        var val = Convert(e.Value?.ToString());
        if (DebounceInterval > 0 && OnDebounceIntervalElapsed.HasDelegate)
        {
            // Keep @bind-Value in sync so parent re-renders do not wipe keystrokes.
            await ValueChanged.InvokeAsync(val);
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(async _ =>
            {
                await InvokeAsync(async () =>
                {
                    if (_disposed) return;

                    await OnDebounceIntervalElapsed.InvokeAsync(val);
                    Validate(val);
                    StateHasChanged();
                });
            }, null, DebounceInterval, Timeout.Infinite);
            return;
        }
        if (!Immediate) return;
        await ValueChanged.InvokeAsync(val);
        Validate(val);
    }

    private async Task OnChange(ChangeEventArgs e)
    {
        var val = Convert(e.Value?.ToString());
        await ValueChanged.InvokeAsync(val);
        Validate(val);
    }

    private TValue? Convert(string? raw)
    {
        if (raw is null) return default;
        try { return (TValue)System.Convert.ChangeType(raw, typeof(TValue)); }
        catch { return default; }
    }

    private void Validate(TValue? val)
    {
        if (Required && val is null or "")
        {
            _hasError = true;
            _errorText = string.IsNullOrEmpty(RequiredError) ? "Required" : RequiredError;
            return;
        }

        if (Validation is not null)
        {
            var error = Validation(val);
            if (error is not null)
            {
                _hasError = true;
                _errorText = error;
                return;
            }
        }

        _hasError = false;
    }

    public void Dispose()
    {
        _disposed = true;
        _debounceTimer?.Dispose();
    }

    private void TogglePasswordReveal() => _passwordRevealed = !_passwordRevealed;

    private async Task ClearAsync()
    {
        await ValueChanged.InvokeAsync(default);
        Validate(default);
    }
}

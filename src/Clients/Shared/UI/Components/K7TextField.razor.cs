using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7TextField<TValue> : IAsyncDisposable
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
    private IJSObjectReference? _module;
    private bool _hasError;
    private string _errorText = "";
    private Timer? _debounceTimer;
    private bool _disposed;
    private bool _passwordRevealed;
    private int _selectionStart;
    private int _selectionEnd;
    private bool _hasCapturedSelection;
    private int? _pendingCaret;

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
        if (firstRender && !_disposed)
        {
            try
            {
                _module = await JS.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/K7.Clients.Shared.UI/js/k7TextField.js");
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
            }

            if (Autofocus && !Disabled)
                await EnsureEditModeAsync();
        }

        if (_pendingCaret is int caret && _module is not null && !_disposed)
        {
            _pendingCaret = null;
            try
            {
                await _module.InvokeVoidAsync("setSelection", _inputRef, caret, caret);
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
            }
        }
    }

    /// <summary>
    /// Inserts text at the current (or last known) caret / selection, then restores focus.
    /// </summary>
    public async Task InsertAtCursorAsync(string text)
    {
        if (_disposed || string.IsNullOrEmpty(text))
            return;

        var current = Value?.ToString() ?? "";
        var start = _hasCapturedSelection
            ? Math.Clamp(_selectionStart, 0, current.Length)
            : current.Length;
        var end = _hasCapturedSelection
            ? Math.Clamp(_selectionEnd, start, current.Length)
            : current.Length;

        if (_module is not null)
        {
            try
            {
                var live = await _module.InvokeAsync<TextFieldSelection>("getSelection", _inputRef);
                if (live is not null)
                {
                    // Prefer live DOM value so uncommitted keystrokes are not lost.
                    current = live.Value ?? current;
                    var liveStart = Math.Clamp(live.Start, 0, current.Length);
                    var liveEnd = Math.Clamp(live.End, liveStart, current.Length);

                    // After blur, browsers often reset selection to 0. Keep the last caret then.
                    if (_hasCapturedSelection
                        && liveStart == 0
                        && liveEnd == 0
                        && (_selectionStart > 0 || _selectionEnd > 0))
                    {
                        start = Math.Clamp(_selectionStart, 0, current.Length);
                        end = Math.Clamp(_selectionEnd, start, current.Length);
                    }
                    else
                    {
                        start = liveStart;
                        end = liveEnd;
                        _hasCapturedSelection = true;
                    }
                }

                var inserted = await _module.InvokeAsync<TextFieldInsertResult>(
                    "insertText", _inputRef, text, start, end);
                if (inserted is not null)
                {
                    current = inserted.Value ?? current;
                    start = inserted.Caret;
                    end = inserted.Caret;
                }
                else
                {
                    current = string.Concat(current.AsSpan(0, start), text, current.AsSpan(end));
                    start += text.Length;
                    end = start;
                }
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
                current = string.Concat(current.AsSpan(0, start), text, current.AsSpan(end));
                start += text.Length;
                end = start;
            }
        }
        else
        {
            current = string.Concat(current.AsSpan(0, start), text, current.AsSpan(end));
            start += text.Length;
            end = start;
        }

        _selectionStart = start;
        _selectionEnd = end;
        _pendingCaret = start;

        var converted = Convert(current);
        Value = converted;
        await ValueChanged.InvokeAsync(converted);
        Validate(converted);
        StateHasChanged();
    }

    private async Task HandleFocusAsync(FocusEventArgs e)
    {
        // Activatable inputs can be refocused by spatial-nav without edit mode
        // (readonly). Autofocus means the user should always be able to type.
        if (Autofocus && !Disabled)
            await EnsureEditModeAsync();

        await CaptureSelectionAsync();

        if (OnFocus.HasDelegate)
            await OnFocus.InvokeAsync(e);
    }

    private async Task HandleFocusOutAsync(FocusEventArgs e)
    {
        await CaptureSelectionAsync();
        if (OnFocusOut.HasDelegate)
            await OnFocusOut.InvokeAsync(e);
    }

    private async Task HandleKeyUpAsync(KeyboardEventArgs e)
    {
        await CaptureSelectionAsync();
        if (OnKeyUp.HasDelegate)
            await OnKeyUp.InvokeAsync(e);
    }

    private async Task HandleClickAsync(MouseEventArgs e)
    {
        await CaptureSelectionAsync();
        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync(e);
    }

    private async Task CaptureSelectionAsync()
    {
        if (_disposed || _module is null)
            return;

        try
        {
            var live = await _module.InvokeAsync<TextFieldSelection>("getSelection", _inputRef);
            if (live is null)
                return;

            var length = (live.Value ?? Value?.ToString() ?? "").Length;
            _selectionStart = Math.Clamp(live.Start, 0, length);
            _selectionEnd = Math.Clamp(live.End, _selectionStart, length);
            _hasCapturedSelection = true;
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
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
        await CaptureSelectionAsync();
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

    private void TogglePasswordReveal() => _passwordRevealed = !_passwordRevealed;

    private async Task ClearAsync()
    {
        await ValueChanged.InvokeAsync(default);
        Validate(default);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _debounceTimer?.Dispose();

        var module = _module;
        _module = null;
        if (module is not null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
            }
        }
    }

    private static bool IsBenignJsFailure(Exception ex) =>
        ex is JSDisconnectedException or ObjectDisposedException or JSException or InvalidOperationException;

    private sealed class TextFieldSelection
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string? Value { get; set; }
    }

    private sealed class TextFieldInsertResult
    {
        public string? Value { get; set; }
        public int Caret { get; set; }
    }
}

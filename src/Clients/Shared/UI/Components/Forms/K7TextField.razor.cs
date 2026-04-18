using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components.Forms;

public partial class K7TextField<TValue> : IDisposable
{
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
    [Parameter] public string AdornmentName { get; set; } = "";
    [Parameter] public EventCallback OnAdornmentClick { get; set; }
    [Parameter] public int DebounceInterval { get; set; }
    [Parameter] public EventCallback<TValue?> OnDebounceIntervalElapsed { get; set; }
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public string HelperText { get; set; } = "";
    [Parameter] public bool Clearable { get; set; }

    private readonly string _id = $"k7tf-{Guid.NewGuid():N}";
    private bool _hasError;
    private string _errorText = "";
    private Timer? _debounceTimer;

    private async Task OnInput(ChangeEventArgs e)
    {
        var val = Convert(e.Value?.ToString());
        if (DebounceInterval > 0 && OnDebounceIntervalElapsed.HasDelegate)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(async _ =>
            {
                await InvokeAsync(async () =>
                {
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
        }
        else
        {
            _hasError = false;
        }
    }

    public void Dispose() => _debounceTimer?.Dispose();

    private async Task ClearAsync()
    {
        await ValueChanged.InvokeAsync(default);
        Validate(default);
    }
}

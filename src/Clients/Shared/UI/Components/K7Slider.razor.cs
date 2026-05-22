using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Slider<TValue>
{
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }
    [Parameter] public TValue? Min { get; set; }
    [Parameter] public TValue? Max { get; set; }
    [Parameter] public TValue? Step { get; set; }
    [Parameter] public bool Immediate { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Vertical { get; set; }
    [Parameter] public string Class { get; set; } = "";

    private TValue? _currentValue;
    private TValue? _lastParameterValue;

    protected override void OnParametersSet()
    {
        if (!EqualityComparer<TValue>.Default.Equals(Value, _lastParameterValue))
        {
            _currentValue = Value;
            _lastParameterValue = Value;
        }
    }

    private static string? Fmt(TValue? v) => v is IFormattable f
        ? f.ToString(null, CultureInfo.InvariantCulture)
        : v?.ToString();

    private string? MinString => Fmt(Min);
    private string? MaxString => Fmt(Max);
    private string? StepString => Fmt(Step);
    private string? CurrentValueString => Fmt(_currentValue);

    private async Task OnInput(ChangeEventArgs e)
    {
        var val = Parse(e);
        if (val is null) return;
        _currentValue = val;
        if (Immediate) await ValueChanged.InvokeAsync(val);
    }

    private async Task OnChange(ChangeEventArgs e)
    {
        var val = Parse(e);
        if (val is null) return;
        _currentValue = val;
        await ValueChanged.InvokeAsync(val);
    }

    private static TValue? Parse(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        if (string.IsNullOrEmpty(raw)) return default;
        try
        {
            return (TValue)Convert.ChangeType(raw, typeof(TValue), CultureInfo.InvariantCulture);
        }
        catch
        {
            return default;
        }
    }
}

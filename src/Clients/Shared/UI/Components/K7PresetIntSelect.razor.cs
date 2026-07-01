using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7PresetIntSelect
{
    private const int CustomMarker = int.MinValue;

    [Parameter] public int Value { get; set; }
    [Parameter] public EventCallback<int> ValueChanged { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public IReadOnlyList<int> Presets { get; set; } = [];
    [Parameter] public string? DisabledLabel { get; set; }
    [Parameter] public int Min { get; set; }
    [Parameter] public int Max { get; set; } = 500;
    [Parameter] public string Class { get; set; } = "";

    private int _selectValue;
    private int _customValue;

    protected override void OnParametersSet()
    {
        _selectValue = Presets.Contains(Value) ? Value : CustomMarker;
        if (_selectValue == CustomMarker)
            _customValue = Value;
    }

    private string FormatSelectDisplay(int value)
    {
        if (value == CustomMarker)
            return Value.ToString();

        if (value == 0 && DisabledLabel is not null)
            return DisabledLabel;

        return value.ToString();
    }

    private string GetHelperText() => string.Format(L["CustomValueRange"], Min, Max);

    private async Task OnSelectChanged(int selected)
    {
        _selectValue = selected;
        if (selected != CustomMarker)
        {
            await ValueChanged.InvokeAsync(selected);
            return;
        }

        _customValue = Presets.Contains(Value) ? Math.Clamp(15, Min, Max) : Value;
        await ApplyCustomValueAsync();
    }

    private async Task OnCustomValueChanged(int value)
    {
        _customValue = value;
        await ApplyCustomValueAsync();
    }

    private string? ValidateCustomValue(int value)
    {
        if (value < Min)
            return string.Format(L["CustomValueTooSmall"], Min);

        if (value > Max)
            return string.Format(L["CustomValueTooLarge"], Max);

        return null;
    }

    private async Task ApplyCustomValueAsync()
    {
        if (ValidateCustomValue(_customValue) is not null)
            return;

        await ValueChanged.InvokeAsync(Math.Clamp(_customValue, Min, Max));
    }
}

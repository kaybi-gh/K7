using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7ByteSizeSelect
{
    private const long CustomMarker = -1;

    [Parameter] public long Value { get; set; }
    [Parameter] public EventCallback<long> ValueChanged { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public IReadOnlyList<long> Presets { get; set; } = [];
    [Parameter] public long MinBytes { get; set; } = 50L * 1024 * 1024;
    [Parameter] public long MaxBytes { get; set; } = 1024L * 1024 * 1024 * 1024;
    [Parameter] public long? DeviceTotalBytes { get; set; }
    [Parameter] public long SiblingBytes { get; set; }
    [Parameter] public string Class { get; set; } = "";

    private long _selectValue;
    private double _customAmount;
    private string _customUnit = "GB";
    private string? _customError;

    private long EffectiveMaxBytes => OfflineStorageLimitHelper.GetEffectiveMaxBytes(
        DeviceTotalBytes, SiblingBytes, MaxBytes, MinBytes);

    private IEnumerable<long> VisiblePresets => Presets.Where(p => p >= MinBytes && p <= EffectiveMaxBytes);

    protected override void OnParametersSet()
    {
        _selectValue = VisiblePresets.Contains(Value) ? Value : CustomMarker;
        if (_selectValue == CustomMarker)
            (_customAmount, _customUnit) = ByteSizeFormatter.SplitForEdit(Value);
    }

    private string FormatSelectDisplay(long value)
    {
        if (value == CustomMarker)
            return ByteSizeFormatter.Format(Value);

        return ByteSizeFormatter.Format(value);
    }

    private async Task OnSelectChanged(long selected)
    {
        _selectValue = selected;
        if (selected != CustomMarker)
        {
            _customError = null;
            if (selected > EffectiveMaxBytes)
            {
                _customError = GetExceedsDeviceMessage();
                return;
            }

            await ValueChanged.InvokeAsync(selected);
            return;
        }

        (_customAmount, _customUnit) = ByteSizeFormatter.SplitForEdit(Value);
        await ApplyCustomValueAsync();
    }

    private async Task OnCustomAmountChanged(double amount)
    {
        _customAmount = amount;
        await ApplyCustomValueAsync();
    }

    private async Task OnCustomUnitChanged(string unit)
    {
        _customUnit = unit;
        await ApplyCustomValueAsync();
    }

    private string? ValidateCustomAmount(double amount)
    {
        if (amount <= 0)
            return L["CustomAmountInvalid"];

        var bytes = ByteSizeFormatter.ToBytes(amount, _customUnit);
        if (bytes < MinBytes)
            return string.Format(L["CustomAmountTooSmall"], ByteSizeFormatter.Format(MinBytes));

        if (bytes > EffectiveMaxBytes)
        {
            if (DeviceTotalBytes is not null && bytes + SiblingBytes > DeviceTotalBytes)
                return GetExceedsDeviceMessage();

            return string.Format(L["CustomAmountTooLarge"], ByteSizeFormatter.Format(EffectiveMaxBytes));
        }

        return null;
    }

    private string GetExceedsDeviceMessage() =>
        string.Format(L["CustomAmountExceedsDevice"], ByteSizeFormatter.Format(DeviceTotalBytes!.Value));

    private async Task ApplyCustomValueAsync()
    {
        var validationError = ValidateCustomAmount(_customAmount);
        _customError = validationError;
        if (validationError is not null)
            return;

        var bytes = ByteSizeFormatter.ToBytes(_customAmount, _customUnit);
        bytes = Math.Clamp(bytes, MinBytes, EffectiveMaxBytes);
        await ValueChanged.InvokeAsync(bytes);
    }
}

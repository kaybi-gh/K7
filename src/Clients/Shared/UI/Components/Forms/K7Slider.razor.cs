using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Forms;

public partial class K7Slider<TValue>
{
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }
    [Parameter] public TValue? Min { get; set; }
    [Parameter] public TValue? Max { get; set; }
    [Parameter] public TValue? Step { get; set; }
    [Parameter] public bool Immediate { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Class { get; set; } = "";

    private async Task OnInput(ChangeEventArgs e)
    {
        if (!Immediate) return;
        await NotifyChanged(e);
    }

    private Task OnChange(ChangeEventArgs e) => NotifyChanged(e);

    private async Task NotifyChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        if (string.IsNullOrEmpty(raw)) return;
        try
        {
            var val = (TValue)System.Convert.ChangeType(raw, typeof(TValue));
            await ValueChanged.InvokeAsync(val);
        }
        catch { }
    }
}

using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Select<TValue>
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }
    [Parameter] public Func<TValue?, string>? ToStringFunc { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string Placeholder { get; set; } = "";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Clearable { get; set; }
    [Parameter] public bool FullWidth { get; set; } = true;
    [Parameter] public string Variant { get; set; } = "outlined";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";

    private readonly string _id = $"k7sel-{Guid.NewGuid():N}";

    private async Task OnChange(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        if (string.IsNullOrEmpty(raw) && Clearable)
        {
            await ValueChanged.InvokeAsync(default);
            return;
        }
        try
        {
            var targetType = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
            TValue? val;
            if (targetType.IsEnum)
                val = (TValue?)Enum.Parse(targetType, raw!);
            else
                val = (TValue?)System.Convert.ChangeType(raw, targetType);
            await ValueChanged.InvokeAsync(val);
        }
        catch { }
    }
}

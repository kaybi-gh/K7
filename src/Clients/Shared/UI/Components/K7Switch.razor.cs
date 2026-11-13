using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Switch
{
    [Parameter] public bool Value { get; set; }
    [Parameter] public EventCallback<bool> ValueChanged { get; set; }
    [Parameter] public string Label { get; set; } = "";

    /// <summary>Accessible name when the visible label lives outside the switch (e.g. a setting row).</summary>
    [Parameter] public string? AriaLabel { get; set; }

    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Class { get; set; } = "";

    private Task OnChange(ChangeEventArgs e)
        => ValueChanged.InvokeAsync(e.Value is true);
}

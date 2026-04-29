using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7IconToggleButton
{
    /// <summary>Icon name (Phosphor) used in both states when only one icon is needed.</summary>
    [Parameter] public string Icon { get; set; } = "";

    /// <summary>Icon name shown when Value is true. Takes precedence over Icon.</summary>
    [Parameter] public string ActiveIcon { get; set; } = "";

    [Parameter] public bool Value { get; set; }
    [Parameter] public EventCallback<bool> ValueChanged { get; set; }

    /// <summary>CSS class added when Value is true. Defaults to k7-icon-btn--active.</summary>
    [Parameter] public string ActiveClass { get; set; } = "k7-icon-btn--active";

    [Parameter] public string AriaLabel { get; set; } = "";
    [Parameter] public string? ActiveAriaLabel { get; set; }

    [Parameter] public string Size { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Type { get; set; } = "button";
    [Parameter] public bool Disabled { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private async Task Toggle()
    {
        Value = !Value;
        await ValueChanged.InvokeAsync(Value);
    }
}

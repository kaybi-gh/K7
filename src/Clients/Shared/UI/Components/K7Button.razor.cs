using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Button
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Variant { get; set; } = "filled";
    [Parameter] public string Color { get; set; } = "";
    [Parameter] public string Size { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public string Type { get; set; } = "button";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string StartIcon { get; set; } = "";
    [Parameter] public string EndIcon { get; set; } = "";
    [Parameter] public string Href { get; set; } = "";
    [Parameter] public string AriaLabel { get; set; } = "";
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }
}

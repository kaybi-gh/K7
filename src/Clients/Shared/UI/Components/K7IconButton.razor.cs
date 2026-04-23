using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7IconButton
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Icon { get; set; } = "";
    [Parameter] public string Color { get; set; } = "";
    [Parameter] public string Size { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Type { get; set; } = "button";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Href { get; set; } = "";
    [Parameter] public string AriaLabel { get; set; } = "";
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }
}

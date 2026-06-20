using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Image
{
    [Parameter] public string Src { get; set; } = "";
    [Parameter] public string Alt { get; set; } = "";
    [Parameter] public string ObjectFit { get; set; } = "";
    [Parameter] public int Width { get; set; }
    [Parameter] public int Height { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public string Loading { get; set; } = "lazy";
    [Parameter] public bool Fluid { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }
}

using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Base;

public partial class K7Skeleton
{
    [Parameter] public string Shape { get; set; } = "rect";
    [Parameter] public string Width { get; set; } = "";
    [Parameter] public string Height { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
}

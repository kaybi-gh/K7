using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Base;

public partial class K7Avatar
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Image { get; set; } = "";
    [Parameter] public string Alt { get; set; } = "";
    [Parameter] public string Size { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
}

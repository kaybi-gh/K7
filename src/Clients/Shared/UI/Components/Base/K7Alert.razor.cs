using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Base;

public partial class K7Alert
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public K7Severity Severity { get; set; } = K7Severity.Normal;
    [Parameter] public string Icon { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Variant { get; set; } = "";
    [Parameter] public bool NoIcon { get; set; }
}

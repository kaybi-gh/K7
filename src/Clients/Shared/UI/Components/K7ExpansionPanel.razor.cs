using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7ExpansionPanel
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? TitleContent { get; set; }
    [Parameter] public string Text { get; set; } = "";
    [Parameter] public bool Expanded { get; set; }
    [Parameter] public string Class { get; set; } = "";
}

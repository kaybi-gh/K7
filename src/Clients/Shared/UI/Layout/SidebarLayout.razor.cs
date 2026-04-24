using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Layout;

public partial class SidebarLayout
{
    [Parameter, EditorRequired] public RenderFragment SidebarContent { get; set; } = default!;
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; } = default!;
}

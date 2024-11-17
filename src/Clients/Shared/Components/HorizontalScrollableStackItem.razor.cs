using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.Components;

public partial class HorizontalScrollableStackItem
{

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
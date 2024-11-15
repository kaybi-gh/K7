using Microsoft.AspNetCore.Components;

namespace MediaClient.Shared.Components;

public partial class HorizontalScrollableStackItem
{

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
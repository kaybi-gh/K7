using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class CarouselItem
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? Class { get; set; }
}

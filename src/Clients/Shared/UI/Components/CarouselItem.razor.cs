using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class CarouselItem
{
    [CascadingParameter] private CarouselSlideWindow? SlideWindow { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? ItemId { get; set; }
    [Parameter] public int? Index { get; set; }
    [Parameter] public bool InitialFocus { get; set; }

    private bool _renderContent => SlideWindow?.ShouldRender(Index) ?? true;

    private string _itemClass =>
        _renderContent
            ? $"carousel-item {Class}"
            : $"carousel-item carousel-item--placeholder {Class}";

    private Dictionary<string, object>? _initialFocusAttributes =>
        InitialFocus ? new Dictionary<string, object> { ["data-initial-focus"] = true } : null;
}

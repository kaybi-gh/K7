using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.Shared.UI.Components;

public sealed class CarouselSlideWindow
{
    public bool Enabled { get; set; }
    public int First { get; set; }
    public int Last { get; set; } = CarouselVirtualWindow.DefaultInitialVisibleCount - 1;

    public bool ShouldRender(int? index) =>
        !Enabled || index is null || CarouselVirtualWindow.Contains(First, Last, index.Value);
}

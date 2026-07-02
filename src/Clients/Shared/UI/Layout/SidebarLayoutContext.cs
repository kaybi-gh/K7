namespace K7.Clients.Shared.UI.Layout;

public sealed class SidebarLayoutContext(bool isDesktopCollapsed)
{
    public bool IsDesktopCollapsed { get; } = isDesktopCollapsed;
}

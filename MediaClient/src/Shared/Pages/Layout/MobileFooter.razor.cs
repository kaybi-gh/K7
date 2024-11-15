namespace MediaClient.Shared.Pages.Layout;

public partial class MobileFooter
{
    private bool _mobileLibraryDrawerOpen = false;

    private void ToggleMobileLibraryDrawer()
    {
        _mobileLibraryDrawerOpen = !_mobileLibraryDrawerOpen;
    }
}
using MediaClient.Shared.Services;
using MudBlazor;

namespace MediaClient.Shared.Pages.Layout;

public partial class Sidebar
{
    private DrawerVariant _variant = DrawerVariant.Mini;
    string _debouncedText = "";

    protected override void OnInitialized()
    {
        SidebarService.IsOpenOnChange += StateHasChanged;
    }

    public void Dispose()
    {
        SidebarService.IsOpenOnChange -= StateHasChanged;
    }

    private void ToggleDrawerVariant(bool isBreakpointXs)
    {
        if (isBreakpointXs)
        {
            _variant = DrawerVariant.Temporary;
            StateHasChanged();
            SidebarService.IsOpen = false;
        }
        else
        {
            _variant = DrawerVariant.Mini;
        }
        StateHasChanged();
    }

    private void Search()
    {
        
    }
}
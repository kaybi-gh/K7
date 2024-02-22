using MediaClient.Shared.Services;

namespace MediaClient.Shared.Pages.Layout;

public partial class Header
{
    protected override void OnInitialized()
    {
        SidebarService.IsOpenOnChange += StateHasChanged;
    }

    public void Dispose()
    {
        SidebarService.IsOpenOnChange -= StateHasChanged;
    }
}
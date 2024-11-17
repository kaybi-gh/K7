using K7.Clients.Shared.Services;

namespace K7.Clients.Shared.Pages.Layout;

public partial class Sidebar
{
    string _debouncedText = "";

    protected override void OnInitialized()
    {
        SidebarService.IsOpenOnChange += StateHasChanged;
    }

    public void Dispose()
    {
        SidebarService.IsOpenOnChange -= StateHasChanged;
    }

    private void Search()
    {
        
    }
}
using MediaClient.Shared.Services;

namespace MediaClient.Shared.Pages.Layout;

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
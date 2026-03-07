using K7.Clients.Shared.Services;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Pages.Layout;

public partial class Sidebar
{
    string _debouncedText = "";
    private DotNetObjectReference<Sidebar>? _dotNetRef;

    protected override void OnInitialized()
    {
        SidebarService.IsOpenOnChange += StateHasChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("SpatialNavigation.setSidebarCallback", _dotNetRef);
        }
    }

    [JSInvokable]
    public void SetSidebarOpen(bool open)
    {
        if (SidebarService.IsOpen != open)
        {
            SidebarService.IsOpen = open;
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        SidebarService.IsOpenOnChange -= StateHasChanged;
        _dotNetRef?.Dispose();
    }

    private void Search()
    {
        
    }

    private async Task Login()
    {
        await CustomAuthenticationStateProvider.LoginAsync();
    }

    private async void Logout()
    {
        await CustomAuthenticationStateProvider.LogoutAsync();
    }
}

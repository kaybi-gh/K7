using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class Settings
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IUserAdminService UserService { get; set; } = default!;

    [Parameter] public string? Tab { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(Tab))
        {
            NavigationManager.NavigateTo($"/settings/{Tab}", replace: true);
            return;
        }

        var me = await UserService.GetCurrentUserAsync();
        var defaultTab = me?.IsGuest == true ? "general" : "account";
        NavigationManager.NavigateTo($"/settings/{defaultTab}?sidebar=open", replace: true);
    }
}

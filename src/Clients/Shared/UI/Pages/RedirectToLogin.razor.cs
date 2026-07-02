using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class RedirectToLogin
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILocalUserService LocalUserService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;

    protected override void OnInitialized()
    {
        var users = LocalUserService.GetAll();
        var target = users.Count > 0 && DeviceService.GetClientType() != ClientType.Web ? "select-user" : "welcome";
        var forceLoad = target == "welcome" && DeviceService.GetClientType() == ClientType.Web;
        Navigation.NavigateTo($"{target}?returnUrl={Uri.EscapeDataString(Navigation.Uri)}", forceLoad);
    }
}

using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class RedirectToLogin
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILocalUserService LocalUserService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;

    protected override void OnInitialized()
    {
        var users = LocalUserService.GetAll();
        var isWeb = DeviceService.GetClientType() == ClientType.Web;
        var returnUrl = Uri.EscapeDataString(Navigation.Uri);

        string target;
        var forceLoad = false;

        if (users.Count > 0 && !isWeb)
        {
            target = $"/select-profile?returnUrl={returnUrl}";
        }
        else if (isWeb)
        {
            target = $"/welcome?returnUrl={returnUrl}";
            forceLoad = true;
        }
        else if (DeviceService.CachedDeviceType == DeviceType.TV
            && CachedGuestAccess.TryGetEnabled(DeviceStorage) == false)
        {
            target = $"/linkdevice?returnUrl={returnUrl}";
        }
        else
        {
            target = $"/welcome?returnUrl={returnUrl}";
        }

        Navigation.NavigateTo(target, forceLoad, replace: true);
    }
}

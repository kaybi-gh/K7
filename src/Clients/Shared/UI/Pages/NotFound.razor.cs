using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.UI.Pages;

public partial class NotFound
{
    protected override void OnInitialized()
    {
        var isWeb = DeviceService.GetClientType() == ClientType.Web;
        var returnUrl = Uri.EscapeDataString(Navigation.Uri);

        if (!isWeb && LocalUserService.GetAll().Count > 0)
        {
            Navigation.NavigateTo($"/select-profile?returnUrl={returnUrl}", replace: true);
            return;
        }

        if (isWeb)
        {
            Navigation.NavigateTo($"/welcome?returnUrl={returnUrl}", forceLoad: true, replace: true);
            return;
        }

        Navigation.NavigateTo($"/welcome?returnUrl={returnUrl}", replace: true);
    }
}

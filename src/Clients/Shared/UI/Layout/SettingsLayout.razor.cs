using K7.Clients.Shared.Interfaces;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Layout;

public partial class SettingsLayout
{
    [Inject] private IUserAdminService UserService { get; set; } = default!;

    private bool _isGuest;

    protected override async Task OnInitializedAsync()
    {
        var me = await UserService.GetCurrentUserAsync();
        if (me is not null)
        {
            _isGuest = me.IsGuest;
        }
    }
}

using K7.Clients.Shared.Interfaces;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Layout;

public partial class SettingsLayout
{
    private static readonly string[] MyContentRoutes = ["/settings/libraries", "/settings/social", "/settings/hidden"];
    private static readonly string[] ExperienceRoutes =
    [
        "/settings/general",
        "/settings/home-layout",
        "/settings/navigation",
        "/settings/audio-player",
        "/settings/video-playback"
    ];
    private static readonly string[] WatchTogetherRoutes =
    [
        "/settings/syncplay",
        "/settings/shared-profiles",
        "/settings/viewing-groups"
    ];

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

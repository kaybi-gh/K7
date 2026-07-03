using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Users;

public partial class UsersDirectoryPage
{
    [Inject] private ISocialUserService SocialUserService { get; set; } = default!;

    private bool _loading = true;
    private List<SocialUserDirectoryEntryDto> _entries = [];

    protected override async Task OnInitializedAsync()
    {
        _entries = (await SocialUserService.GetSocialUserDirectoryAsync()).ToList();
        _loading = false;
    }
}

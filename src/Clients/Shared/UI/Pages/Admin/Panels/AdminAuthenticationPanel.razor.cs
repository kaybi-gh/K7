using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminAuthenticationPanel
{
    [Inject] private IServerInfoService K7ServerService { get; set; } = default!;

    private AuthenticationInfoDto? _authInfo;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _authInfo = await K7ServerService.GetAuthenticationInfoAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }
}

using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminGeneralPanel
{
    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;

    private string _defaultLanguage = "en";
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var serverInfo = await ServerInfoService.GetServerInfoAsync();
            if (serverInfo is not null)
            {
                _defaultLanguage = serverInfo.DefaultLanguage;
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnDefaultLanguageChanged(string language)
    {
        _defaultLanguage = language;
        await ServerInfoService.UpdateDefaultLanguageAsync(language);
    }
}

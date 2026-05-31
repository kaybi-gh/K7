using System.Reflection;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsAboutPage
{
    private string? _serverVersion;
    private string _clientVersion = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        _clientVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";

        var aboutInfo = await ServerInfoService.GetAboutInfoAsync();
        _serverVersion = aboutInfo?.ServerVersion;
    }
}

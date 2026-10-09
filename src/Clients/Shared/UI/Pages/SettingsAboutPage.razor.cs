using System.Reflection;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsAboutPage
{
    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;

    private string? _serverVersion;
    private string _clientVersion = string.Empty;

    private string ServerVersionDisplay => GetVersionWithoutHash(_serverVersion) ?? "...";
    private string ClientVersionDisplay => GetVersionWithoutHash(_clientVersion) ?? "...";

    protected override async Task OnInitializedAsync()
    {
        _clientVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";

        try
        {
            var aboutInfo = await ServerInfoService.GetAboutInfoAsync();
            _serverVersion = aboutInfo?.ServerVersion;
        }
        catch
        {
            // Server version unavailable (offline, outdated server, or non-JSON response)
        }
    }

    private static string? GetVersionWithoutHash(string? version)
    {
        if (string.IsNullOrEmpty(version)) return version;
        var plusIndex = version.IndexOf('+');
        return plusIndex > 0 ? version[..plusIndex] : version;
    }
}

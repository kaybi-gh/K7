using System.Reflection;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Constants;
using K7.Shared;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsAboutPage
{
    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private const string RepositoryUrl = "https://github.com/kaybi-gh/K7";
    private const string UserDocsUrl = "https://github.com/kaybi-gh/K7/blob/main/docs/user/guide.md";
    private const string AdminDocsUrl = "https://github.com/kaybi-gh/K7/blob/main/docs/README.md";

    private string? _serverVersion;
    private string _clientVersion = string.Empty;
    private bool _isAdmin;
    private bool _perfTraceEnabled = PerfTrace.IsEnabled;

    private string ServerVersionDisplay => GetVersionWithoutHash(_serverVersion) ?? "...";
    private string ClientVersionDisplay => GetVersionWithoutHash(_clientVersion) ?? "...";
    private string? ServerReleaseNotesUrl => GetReleaseNotesUrl(_serverVersion);
    private string? ClientReleaseNotesUrl => GetReleaseNotesUrl(_clientVersion);
    private string DocsUrl => _isAdmin ? AdminDocsUrl : UserDocsUrl;

    protected override async Task OnInitializedAsync()
    {
        var role = await FeatureAccess.GetRoleAsync();
        _isAdmin = role == Roles.Administrator;

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

    private async Task OnPerfTraceChangedAsync(bool enabled)
    {
        _perfTraceEnabled = enabled;
        DeviceStorage.Set(PreferenceKeys.PERF_TRACE_ENABLED, enabled);
        PerfTrace.SetEnabled(enabled);
        await PerfTrace.SyncJsAsync(JS);
    }

    private static string? GetVersionWithoutHash(string? version)
    {
        if (string.IsNullOrEmpty(version)) return version;
        var plusIndex = version.IndexOf('+');
        return plusIndex > 0 ? version[..plusIndex] : version;
    }

    private static string? GetReleaseNotesUrl(string? version)
    {
        var display = GetVersionWithoutHash(version);
        if (string.IsNullOrWhiteSpace(display) || display == "unknown")
            return null;

        var tag = display.StartsWith('v') ? display : $"v{display}";
        return $"{RepositoryUrl}/releases/tag/{Uri.EscapeDataString(tag)}";
    }
}

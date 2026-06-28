using System.Reflection;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Devices;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsAboutPage
{
    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;

    private string? _serverVersion;
    private string _clientVersion = string.Empty;
    private DeviceType _deviceType;
    private DeviceCodecSummaryDto? _codecSummary;
    private bool? _hdrSupport;

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

        _deviceType = await DeviceService.GetDeviceTypeAsync();

        try
        {
            _hdrSupport = await DeviceService.GetHdrSupportAsync();
        }
        catch
        {
            _hdrSupport = null;
        }

        try
        {
            _codecSummary = await DeviceService.GetDeviceCodecSummaryAsync();
        }
        catch
        {
            _codecSummary = new DeviceCodecSummaryDto
            {
                Containers = [],
                AudioCodecs = [],
                VideoCodecs = []
            };
        }
    }

    private static string? GetVersionWithoutHash(string? version)
    {
        if (string.IsNullOrEmpty(version)) return version;
        var plusIndex = version.IndexOf('+');
        return plusIndex > 0 ? version[..plusIndex] : version;
    }
}

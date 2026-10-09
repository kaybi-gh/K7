using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Devices;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsDevicePage
{
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorageService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IServerConnectionService ServerConnectionService { get; set; } = default!;

    private string? _backendUrl;
    private DeviceType? _deviceType;
    private bool? _hdrSupport;
    private DeviceCodecSummaryDto? _codecSummary;
    private bool _infoLoaded;
    private bool _clearingPreferences;

    private string DeviceTypeDisplay => _deviceType switch
    {
        DeviceType.Desktop => L["DeviceDesktop"],
        DeviceType.Phone => L["DevicePhone"],
        DeviceType.Tablet => L["DeviceTablet"],
        DeviceType.TV => L["DeviceTv"],
        DeviceType.Watch => L["DeviceWatch"],
        _ => L["Unavailable"]
    };

    private string HdrDisplay => _hdrSupport switch
    {
        true => S["Yes"],
        false => S["No"],
        _ => L["Unavailable"]
    };

    protected override void OnInitialized()
    {
        _backendUrl = ResolveBackendUrlDisplay();
    }

    protected override async Task OnInitializedAsync()
    {
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

        _infoLoaded = true;
    }

    private string? ResolveBackendUrlDisplay()
    {
        var storedUrl = DeviceStorageService.Get(PreferenceKeys.K7_SERVER_URL);
        if (!string.IsNullOrEmpty(storedUrl))
            return storedUrl;

        return ApiClient.HttpClient.BaseAddress?.AbsoluteUri;
    }

    private async Task ChangeBackendUrl()
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["ChangeServerUrlTitle"],
            L["ChangeServerUrlWarning"],
            yesText: S["Confirm"],
            cancelText: S["Cancel"]);

        if (confirmed is not true)
            return;

        ServerConnectionService.DisconnectAndReset();
    }

    private async Task ClearAllPreferencesAsync()
    {
        if (_clearingPreferences)
            return;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["ClearAllPreferencesTitle"],
            L["ClearAllPreferencesMessage"],
            yesText: L["ClearAllPreferences"],
            cancelText: S["Cancel"]);

        if (confirmed is not true)
            return;

        _clearingPreferences = true;
        try
        {
            DeviceStorageService.ClearAllPreferences();
            await JSRuntime.InvokeVoidAsync("K7.clearSavedTheme");
            Snackbar.Add(L["ClearPreferencesSuccess"], K7Severity.Success);
            NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _clearingPreferences = false;
        }
    }
}

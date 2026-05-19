using K7.Clients.MAUI.Constants;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using SharedPreferenceKeys = K7.Shared.PreferenceKeys;

namespace K7.Clients.MAUI.Services;

public class K7ServerManagerService : IServerConnectionService
{
    public event EventHandler<string>? BaseAddressUpdated;
    private readonly IK7ServerService _k7ServerService;
    private readonly IOptionsMonitor<OpenIddictClientOptions> _openIddictOptions;
    private readonly OpenIddictClientService _openIddictClientService;
    private readonly IDeviceStorageService _deviceStorageService;
    private readonly ILocalUserService _localUserService;

    public K7ServerManagerService(
        IK7ServerService k7ServerService,
        IOptionsMonitor<OpenIddictClientOptions> openIddictOptions,
        OpenIddictClientService openIddictClientService,
        IDeviceStorageService deviceStorageService,
        ILocalUserService localUserService)
    {
        _k7ServerService = k7ServerService;
        _openIddictOptions = openIddictOptions;
        _openIddictClientService = openIddictClientService;
        _deviceStorageService = deviceStorageService;
        _localUserService = localUserService;
    }

    public void UpdateBaseAddress(string baseAddress)
    {
        var newUri = new Uri(baseAddress);
        if (_k7ServerService.HttpClient.BaseAddress?.OriginalString == newUri.OriginalString)
            return;

        _k7ServerService.HttpClient.BaseAddress = newUri;

        BaseAddressUpdated?.Invoke(this, baseAddress);
    }

    /// <summary>
    /// Adds the K7 OpenIddict client registration dynamically if it was not configured at startup.
    /// This allows the app to transition seamlessly after first-launch setup without a process restart.
    /// </summary>
    public void EnsureOpenIddictRegistration(string serverUrl)
    {
        var options = _openIddictOptions.CurrentValue;
        var alreadyRegistered = options.Registrations.Exists(
            r => string.Equals(r.ProviderName, "K7", StringComparison.Ordinal));

        if (alreadyRegistered)
            return;

        var registration = MauiProgram.CreateK7Registration(serverUrl);

        // Replicate the PostConfigure normalization: set up ConfigurationManager
        // so OpenIddict can resolve the server's discovery document at runtime.
        registration.ConfigurationEndpoint = new Uri(
            new Uri(serverUrl, UriKind.Absolute),
            ".well-known/openid-configuration");

        registration.ConfigurationManager = new ConfigurationManager<OpenIddictConfiguration>(
            registration.ConfigurationEndpoint.AbsoluteUri,
            new OpenIddictClientRetriever(_openIddictClientService, registration))
        {
            AutomaticRefreshInterval = ConfigurationManager<OpenIddictConfiguration>.DefaultAutomaticRefreshInterval,
            RefreshInterval = ConfigurationManager<OpenIddictConfiguration>.DefaultRefreshInterval
        };

        options.Registrations.Add(registration);
    }

    public void RemoveRegisteredBackendUrl()
    {
        // Clear all server-related preferences
        Preferences.Remove(PreferenceKeys.K7_SERVER_URL);
        _deviceStorageService.Remove(SharedPreferenceKeys.ACCESS_TOKEN);
        _deviceStorageService.Remove(SharedPreferenceKeys.REFRESH_TOKEN);
        _deviceStorageService.Remove(SharedPreferenceKeys.SERVER_INFO);
        _deviceStorageService.Remove(SharedPreferenceKeys.DEVICE_ATTACHED_USER_ID);
        _deviceStorageService.Remove(SharedPreferenceKeys.LAST_ACTIVE_USER_ID);

        // Clear local users
        foreach (var user in _localUserService.GetAll())
            _localUserService.Remove(user.IdentityUserId);

        // Clear HTTP auth state
        _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;

        // Remove any existing K7 registration so a new one can be added for a different server
        var options = _openIddictOptions.CurrentValue;
        options.Registrations.RemoveAll(
            r => string.Equals(r.ProviderName, "K7", StringComparison.Ordinal));

        ((App)Application.Current!).Restart();
    }

    public void DisconnectAndReset() => RemoveRegisteredBackendUrl();
}

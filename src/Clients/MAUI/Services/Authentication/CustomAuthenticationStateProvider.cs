using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using K7.Clients.MAUI.Data;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Shared;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Abstractions.OpenIddictExceptions;

#if WINDOWS
using K7.Clients.MAUI.Platforms.Windows;
#endif
namespace K7.Clients.MAUI.Services.Authentication;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider, ICustomAuthenticationStateProvider
{
    public event EventHandler? AccessTokenChanged;

    private readonly IServiceProvider _services;
    private readonly IK7ServerService _k7ServerService;
    private readonly IUserAdminService _userAdminService;
    private readonly IDeviceApiService _deviceApiService;
    private readonly IDeviceStorageService _deviceStorageService;
    private readonly ILocalUserService _localUserService;
    private readonly ISharedProfileSessionService? _viewingGroupSession;
    private readonly ISharedProfileLocalCache? _viewingGroupCache;
    private readonly K7HubClient? _hubClient;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private int _provisionalApplied;
    private int _initialized; // 0 = full restore not started, 1 = started
    private Task? _restoreTask;
    private readonly object _initLock = new();
    private static readonly AsyncLocal<bool> RestoreOnCallStack = new();
    /// <summary>
    /// Last refresh token successfully redeemed in this process. Redeeming it again triggers
    /// OpenIddict rolling-token reuse detection and revokes the whole authorization family.
    /// </summary>
    private string? _lastRedeemedRefreshToken;
    /// <summary>
    /// Expiry of the access token in storage, mirrored in memory so the headless restore loop
    /// does not re-read SecureStorage for every check. Null when unknown or cleared.
    /// </summary>
    private DateTimeOffset? _accessTokenExpiresAt;
    private Task<StoredSessionSnapshot?>? _storedSessionPrefetch;

    /// <summary>
    /// Lazy: constructing <see cref="OpenIddictClientService"/> on first auth DI resolve
    /// contended with BlazorWebView boot (~10s on TV). Resolve only for real OIDC work.
    /// </summary>
    private OpenIddictClientService OpenIddict =>
        field ??= _services.GetRequiredService<OpenIddictClientService>();

    /// <summary>
    /// True while cold-start restore is running on this async flow. The auth HTTP handler
    /// must not await <see cref="GetAuthenticationStateAsync"/> in that case or it deadlocks
    /// on nested calls like /api/users/me during token enrichment.
    /// </summary>
    internal bool IsSessionRestoreInProgress => RestoreOnCallStack.Value;

    public CustomAuthenticationStateProvider(
        IServiceProvider services,
        IK7ServerService k7ServerService,
        IUserAdminService userAdminService,
        IDeviceApiService deviceApiService,
        IDeviceStorageService deviceStorageService,
        ILocalUserService localUserService,
        ISharedProfileSessionService? viewingGroupSession = null,
        ISharedProfileLocalCache? viewingGroupCache = null,
        K7HubClient? hubClient = null,
        ILogger<CustomAuthenticationStateProvider>? logger = null)
    {
        _services = services;
        _k7ServerService = k7ServerService;
        _userAdminService = userAdminService;
        _deviceApiService = deviceApiService;
        _deviceStorageService = deviceStorageService;
        _localUserService = localUserService;
        _viewingGroupSession = viewingGroupSession;
        _viewingGroupCache = viewingGroupCache;
        _hubClient = hubClient;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CustomAuthenticationStateProvider>.Instance;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Provisional token only. Do not start OpenIddict DB / refresh here: AuthorizeRouteView
        // and CascadingAuthenticationState call this during Blazor first paint. Full restore
        // starts after AppReady (UI) or via EnsureReadyForHeadlessPlaybackAsync (Auto).
        MauiStartupTrace.Mark("AuthState-first-call");
        EnsureProvisionalApplied();
        MauiStartupTrace.Mark("AuthState-first-return");
        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    private void EnsureProvisionalApplied()
    {
        if (Volatile.Read(ref _provisionalApplied) != 0)
            return;

        lock (_initLock)
        {
            if (_provisionalApplied != 0)
                return;

            TryApplyProvisionalAccessToken();
            Volatile.Write(ref _provisionalApplied, 1);
        }
    }

    /// <summary>
    /// Starts OpenIddict session restore. Call after first paint (UI) or from headless EnsureReady.
    /// </summary>
    internal void EnsureRestoreStarted()
    {
        EnsureProvisionalApplied();

        if (Volatile.Read(ref _initialized) != 0)
            return;

        lock (_initLock)
        {
            if (_initialized != 0)
                return;

            OpenIddictDbBootstrap.Start(_services);
            _restoreTask = TryRestoreSessionAsync();
            Volatile.Write(ref _initialized, 1);
        }
    }

    /// <summary>
    /// UI-only fast path: sign in from the stored session (access token still valid, claims
    /// snapshot) so AuthorizeRouteView can leave Authorizing without OpenIddictDbBootstrap or
    /// the token endpoint. Runs on the Blazor dispatcher during first paint.
    /// </summary>
    private void TryApplyProvisionalAccessToken()
    {
        if (_k7ServerService.HttpClient.BaseAddress is null)
            return;

        StoredSessionSnapshot? stored;
        var prefetch = Interlocked.Exchange(ref _storedSessionPrefetch, null);
        if (prefetch is not null)
        {
            stored = AwaitPrefetchedSession(prefetch);
        }
        else
        {
            if (!MauiSessionRestore.ShouldRestore(_localUserService, serverConfigured: true))
                return;

            stored = ReadStoredSession();
        }

        MauiStartupTrace.Mark("ProvisionalToken-read");
        if (stored is null || !TryRestoreFromStoredSession(stored))
            return;

        MauiStartupTrace.Mark("ProvisionalToken-applied");
        NativeAuthTrace.Write("provisional-access-token", "ok");
    }

    /// <summary>
    /// Reads the stored session on a worker thread ahead of the first
    /// <see cref="GetAuthenticationStateAsync"/>: every SecureStorage read is a synchronous
    /// Keystore round trip (100-300 ms on a TV box) and that first call runs on the main
    /// thread during Blazor first paint. Call once the server URL is applied.
    /// </summary>
    internal void PrefetchStoredSession()
    {
        if (Volatile.Read(ref _provisionalApplied) != 0 || _storedSessionPrefetch is not null)
            return;

        var prefetch = Task.Run<StoredSessionSnapshot?>(() =>
        {
            if (_k7ServerService.HttpClient.BaseAddress is null
                || !MauiSessionRestore.ShouldRestore(_localUserService, serverConfigured: true))
                return null;

            var stored = ReadStoredSession();
            MauiStartupTrace.Mark("ProvisionalToken-prefetched");
            return stored;
        });

        Interlocked.CompareExchange(ref _storedSessionPrefetch, prefetch, null);
    }

    private StoredSessionSnapshot? AwaitPrefetchedSession(Task<StoredSessionSnapshot?> prefetch)
    {
        try
        {
            // Started at CreateWindow, normally long finished; otherwise this costs what the
            // synchronous read would.
            return prefetch.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stored session prefetch failed");
            return null;
        }
    }

    /// <summary>
    /// Android Auto / CarPlay start this process without <c>App.CreateWindow</c>. Wait for
    /// network, restore (or retry a restore that ran before the server URL was applied),
    /// then refresh so ExoPlayer can bake a live Bearer.
    /// </summary>
    public async Task EnsureReadyForHeadlessPlaybackAsync(CancellationToken cancellationToken = default)
    {
        MauiStartupTrace.MarkRepeat("Headless-auth-start");
        await WaitForNetworkHintAsync(cancellationToken);

        for (var attempt = 0; attempt < MauiSessionRestore.HeadlessRestoreMaxAttempts; attempt++)
        {
            MauiStartupTrace.MarkRepeat("Headless-attempt");
            AllowRestoreRetryIfNeeded();
            EnsureRestoreStarted();
            var restore = _restoreTask;
            if (restore is not null)
                await restore.WaitAsync(cancellationToken);

            if (ReadyForHeadlessPlayback())
                return;

            var serverConfigured = _k7ServerService.HttpClient.BaseAddress is not null;
            if (MauiSessionRestore.ShouldRestoreHeadless(_localUserService, serverConfigured)
                && !MauiSessionRestore.ShouldRestore(_localUserService, serverConfigured))
            {
                var lastUser = _localUserService.GetLastActive();
                if (lastUser is not null)
                {
                    RestoreOnCallStack.Value = true;
                    try
                    {
                        await RestoreUserInBackgroundAsync(lastUser);
                    }
                    finally
                    {
                        RestoreOnCallStack.Value = false;
                    }
                }
            }

            if (ReadyForHeadlessPlayback())
                return;

            await TryRefreshAsync(cancellationToken);
            if (ReadyForHeadlessPlayback())
                return;

            if (attempt < MauiSessionRestore.HeadlessRestoreMaxAttempts - 1)
                await Task.Delay(MauiSessionRestore.HeadlessRestoreRetryDelay, cancellationToken);
        }

        MauiStartupTrace.MarkRepeat("Headless-auth-exhausted");
    }

    private bool ReadyForHeadlessPlayback()
    {
        if (!HasUsableOnlineAccessToken())
            return false;

        MauiStartupTrace.MarkRepeat("Headless-auth-usable");
        return true;
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));

        var issuerHost = _k7ServerService.HttpClient.BaseAddress?.Host ?? "-";
        NativeAuthTrace.Write("login-start", issuerHost);
        _logger.LogInformation("Interactive login started for host {IssuerHost}", issuerHost);

        try
        {
            // The interactive flow presents the system browser for sign-in. On iOS/Mac
            // Catalyst that is an ASWebAuthenticationSession (UIKit), which MUST be started
            // on the main thread. Running it via Task.Run presents it from a threadpool
            // thread and raises "UIKit Consistency error: you are calling a UIKit method
            // that can only be invoked from the UI thread." The flow is fully async and
            // yields to the run loop, so presenting on the main thread does not freeze the UI.
            async Task RunInteractiveLoginAsync()
            {
                var authorizeParameters = new Dictionary<string, OpenIddict.Abstractions.OpenIddictParameter>
                {
                    ["prompt"] = "login"
                };
#if WINDOWS
                // OIDC display=page: server swaps the k7:// 302 for /auth/complete
                // so the system browser is not left on /sign-in.
                authorizeParameters["display"] = "page";
#elif LINUX
                // Only with the custom scheme: the loopback 302 must reach the listener directly.
                if (K7.Clients.MAUI.Linux.LinuxAuthRedirect.UsesCustomScheme(
                        Environment.GetEnvironmentVariable(K7.Clients.MAUI.Linux.LinuxAuthRedirect.EnvironmentVariable)))
                    authorizeParameters["display"] = "page";
#endif
                var challenge = await OpenIddict.ChallengeInteractivelyAsync(new()
                {
                    CancellationToken = timeout.Token,
                    ProviderName = "K7",
                    AdditionalAuthorizationRequestParameters = authorizeParameters
                }).ConfigureAwait(false);

                NativeAuthTrace.Write("challenge-ready");
                _logger.LogInformation("Interactive login challenge issued");

                var result = await OpenIddict.AuthenticateInteractivelyAsync(new()
                {
                    CancellationToken = timeout.Token,
                    Nonce = challenge.Nonce
                }).ConfigureAwait(false);

                var accessToken = ResolveInteractiveAccessToken(result);
                NativeAuthTrace.Write(
                    "authenticate-ready",
                    $"access={(string.IsNullOrEmpty(accessToken) ? "no" : "yes")} refresh={(string.IsNullOrEmpty(result.RefreshToken) ? "no" : "yes")}");
                _logger.LogInformation(
                    "Interactive login tokens received access {HasAccess} refresh {HasRefresh}",
                    !string.IsNullOrEmpty(accessToken),
                    !string.IsNullOrEmpty(result.RefreshToken));

                _currentUser = new ClaimsPrincipal(new ClaimsIdentity(result.Principal.Claims, "OpenIddict", Claims.Name, Claims.Role));

                StoreAccessToken(accessToken, ResolveInteractiveAccessTokenExpiry(result));

                if (!string.IsNullOrEmpty(result.RefreshToken))
                    PersistRefreshToken(result.RefreshToken);

                if (!HasPersistedSessionTokens())
                {
                    NativeAuthTrace.Write("persist-missing");
                    _logger.LogWarning("Interactive login produced no persisted session tokens");
                    _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                    ClearStoredTokens();
                    _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;
                }
                else
                {
                    NativeAuthTrace.Write("persist-ok");
                    PersistSessionClaims();
                    await SaveLocalUserFromCurrentUserAsync(timeout.Token).ConfigureAwait(false);
                    await TryAttachCurrentUserToDeviceAsync(timeout.Token).ConfigureAwait(false);
                }
            }

#if IOS || MACCATALYST
            await Microsoft.Maui.ApplicationModel.MainThread
                .InvokeOnMainThreadAsync(RunInteractiveLoginAsync)
                .ConfigureAwait(false);
#else
            // Other platforms: run off the Blazor sync context so the WebView UI stays
            // responsive while the system browser completes the redirect.
            await Task.Run(RunInteractiveLoginAsync, timeout.Token).ConfigureAwait(false);
#endif

            NativeAuthTrace.Write(
                "login-complete",
                _currentUser.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous");
        }
        catch (OperationCanceledException)
        {
            NativeAuthTrace.Write("login-timeout");
            _logger.LogWarning("Interactive login canceled or timed out");
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
        catch (ProtocolException exception) when (exception.Error is Errors.AccessDenied)
        {
            NativeAuthTrace.Write("login-denied");
            _logger.LogInformation("Interactive login denied");
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
        catch (Exception ex)
        {
            NativeAuthTrace.Write("login-error", ex.GetType().Name);
            _logger.LogWarning(ex, "Interactive login failed");
            throw;
        }

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LoginAsGuestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _k7ServerService.HttpClient.PostAsync("api/authentication/guest-login", null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var tokenProp))
            {
                var accessToken = tokenProp.GetString()!;
                DateTimeOffset? expiresAt = null;
                if (root.TryGetProperty("expires_in", out var expiresInProp)
                    && expiresInProp.ValueKind == JsonValueKind.Number
                    && expiresInProp.TryGetInt64(out var expiresInSeconds))
                    expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
                StoreAccessToken(accessToken, expiresAt ?? JwtAccessToken.TryReadExpiry(accessToken));

                _currentUser = new ClaimsPrincipal(new ClaimsIdentity(JwtAccessToken.ReadClaims(accessToken), "OpenIddict", Claims.Name, Claims.Role));
                PersistSessionClaims();
            }
        }
        catch
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LoginWithDeviceCodeAsync(Func<DeviceCodeInfo, Task> onDeviceCodeReceived, CancellationToken cancellationToken = default)
    {
        var challenge = await OpenIddict.ChallengeUsingDeviceAsync(new()
        {
            CancellationToken = cancellationToken,
            ProviderName = "K7"
        });

        await onDeviceCodeReceived(new DeviceCodeInfo(
            challenge.UserCode,
            challenge.VerificationUri.ToString(),
            challenge.VerificationUriComplete?.ToString() ?? challenge.VerificationUri + "?user_code=" + Uri.EscapeDataString(challenge.UserCode),
            DateTimeOffset.UtcNow.Add(challenge.ExpiresIn)));

        var result = await OpenIddict.AuthenticateWithDeviceAsync(new()
        {
            CancellationToken = cancellationToken,
            DeviceCode = challenge.DeviceCode,
            Interval = challenge.Interval,
            Timeout = challenge.ExpiresIn,
            ProviderName = "K7"
        });

        _currentUser = new ClaimsPrincipal(new ClaimsIdentity(result.Principal.Claims, "OpenIddict", Claims.Name, Claims.Role));

        StoreAccessToken(result.AccessToken, result.AccessTokenExpirationDate);

        if (!string.IsNullOrEmpty(result.RefreshToken))
            PersistRefreshToken(result.RefreshToken);

        if (!HasPersistedSessionTokens())
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            ClearStoredTokens();
            _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            return;
        }

        // Device code flow may not return an identity token with the name claim.
        // Fetch user info from the server to enrich the principal.
        if (_currentUser.Identity?.Name is null)
        {
            try
            {
                var serverUser = await _userAdminService.GetCurrentUserAsync(cancellationToken);
                if (serverUser?.UserName is not null)
                {
                    var claims = new List<Claim>(_currentUser.Claims)
                    {
                        new(Claims.Name, serverUser.UserName)
                    };
                    _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "OpenIddict", Claims.Name, Claims.Role));
                }
            }
            catch { }
        }

        PersistSessionClaims();
        await SaveLocalUserFromCurrentUserAsync(cancellationToken);
        await TryAttachCurrentUserToDeviceAsync(cancellationToken);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public Task EndSessionAsync(CancellationToken cancellationToken = default)
    {
        var identityUserId = _currentUser.FindFirst(Claims.Subject)?.Value
            ?? _currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;
        ClearStoredTokens();
        _viewingGroupSession?.ClearActiveGroup();

        // Keep the local profile so SelectProfile can reconnect. Drop the refresh token so
        // we never redeem a revoked RT (OpenIddict rolling reuse).
        if (!string.IsNullOrEmpty(identityUserId))
            _localUserService.ClearRefreshToken(identityUserId);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        return Task.CompletedTask;
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var identityUserId = _currentUser.FindFirst(Claims.Subject)?.Value
            ?? _currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;
        ClearStoredTokens();
        _viewingGroupSession?.ClearActiveGroup();

        if (!string.IsNullOrEmpty(identityUserId))
            _localUserService.Remove(identityUserId);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        return Task.CompletedTask;
    }

    public void SignInOffline(LocalUser user)
    {
        var claims = new List<Claim>
        {
            new(Claims.Subject, user.IdentityUserId),
            new(Claims.Name, user.UserName),
            new(ClaimTypes.Role, "User")
        };

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(ClaimTypes.Email, user.Email));

        _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthIdentity.OfflineAuthenticationType, Claims.Name, ClaimTypes.Role));
        _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;
        RemoveStoredAccessToken();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private async Task TryRestoreSessionAsync()
    {
        RestoreOnCallStack.Value = true;
        try
        {
            // Yield so CreateWindow is not blocked on SecureStorage during the sync prefix.
            await Task.Yield();
            MauiStartupTrace.MarkRepeat("AuthRestore-start");
            if (_k7ServerService.HttpClient.BaseAddress is null)
            {
                // CreateWindow / Android Auto may apply the URL a moment later. Do not
                // consume the one-shot restore so the next caller can actually sign in.
                lock (_initLock)
                {
                    Volatile.Write(ref _initialized, 0);
                    _restoreTask = null;
                }

                return;
            }

            await TryRestoreSessionCoreAsync();
        }
        finally
        {
            RestoreOnCallStack.Value = false;
            MauiStartupTrace.MarkRepeat("AuthRestore-end");
            // Wake CascadingAuthenticationState / AuthorizeRouteView that painted early.
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        }
    }

    private async Task TryRestoreSessionCoreAsync()
    {
        var serverConfigured = _k7ServerService.HttpClient.BaseAddress is not null;
        if (!MauiSessionRestore.ShouldRestore(_localUserService, serverConfigured))
            return;

        var lastUser = _localUserService.GetLastActive();
        if (lastUser is null)
            return;

        // Only skip online restore when there is truly no network.
        // NetworkAccess.Local / ConstrainedInternet are common for self-hosted LAN
        // servers and Android Auto (phone may have no upstream Internet). Treating
        // those as offline wiped the access token and left AA online tabs empty.
        // A brief NetworkAccess.None while the car binds must not drop a still-valid Bearer
        // or block on a 15s refresh timeout.
        if (Connectivity.Current.NetworkAccess == NetworkAccess.None)
        {
            if (MauiSessionRestore.ShouldSignInOfflineWhenDisconnected(HasUsableOnlineAccessToken()))
            {
                SignInOffline(lastUser);
                await RestoreSharedProfileAsync();
            }

            return;
        }

        try
        {
            await OpenIddictDbBootstrap.Ready;
            await RestoreUserInBackgroundAsync(lastUser);
        }
        catch (HttpRequestException)
        {
            FallbackAfterRestoreFailure(lastUser);
        }
        catch (TaskCanceledException)
        {
            FallbackAfterRestoreFailure(lastUser);
        }
        catch
        {
            FallbackAfterRestoreFailure(lastUser);
        }
    }

    /// <summary>
    /// If refresh already produced an online principal + access token, keep it. Only fall
    /// back to offline when restore never managed to sign in.
    /// </summary>
    private void FallbackAfterRestoreFailure(LocalUser lastUser)
    {
        if (_currentUser.Identity?.IsAuthenticated == true
            && !string.IsNullOrEmpty(_deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN)))
            return;

        SignInOffline(lastUser);
    }

    private async Task RestoreUserInBackgroundAsync(LocalUser localUser)
    {
        var refreshToken = _deviceStorageService.Get(PreferenceKeys.REFRESH_TOKEN) ?? localUser.RefreshToken;

        MauiStartupTrace.MarkRepeat("AuthRestore-refresh-start");
        var outcome = await TryRefreshTokenAsync(refreshToken);
        MauiStartupTrace.MarkRepeat("AuthRestore-refresh-end");
        if (outcome == RefreshOutcome.Success)
        {
            await SaveLocalUserFromCurrentUserAsync();
            await RestoreSharedProfileAsync();
            MauiStartupTrace.MarkRepeat("AuthRestore-shared-profile");
            NotifyAuthenticationStateChanged(
                Task.FromResult(new AuthenticationState(_currentUser)));
            return;
        }

        // Invalid grant / revoked family: stay anonymous so AuthorizeRouteView redirects to
        // select-profile. Signing in offline would satisfy [Authorize] with an empty online UI.
        if (MauiSessionRestore.ShouldClearStoredTokens(invalidGrant: outcome == RefreshOutcome.InvalidGrant))
            ClearStoredTokens();

        // Invalid grant / revoked family: stay anonymous so AuthorizeRouteView redirects to
        // select-profile. Signing in offline would satisfy [Authorize] with an empty online UI.
        if (outcome == RefreshOutcome.InvalidGrant)
        {
            if (!string.IsNullOrEmpty(localUser.IdentityUserId))
                _localUserService.ClearRefreshToken(localUser.IdentityUserId);
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(
                Task.FromResult(new AuthenticationState(_currentUser)));
            return;
        }

        // Transient failure with network hiccups: keep an offline session for local media / AA.
        // The refresh token stays so a later retry (car Bluetooth/WiFi coming up) can go online.
        // SignInOffline already drops the access token so ExoPlayer does not keep a dead Bearer.
        SignInOffline(localUser);
        await RestoreSharedProfileAsync();
    }

    private enum RefreshOutcome
    {
        Success,
        TransientFailure,
        InvalidGrant
    }

    /// <summary>Access token plus the expiry and claims snapshot persisted with it.</summary>
    private sealed record StoredSessionSnapshot(string? AccessToken, DateTimeOffset? ExpiresAt, string? ClaimsJson);

    private async Task<RefreshOutcome> TryRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default,
        string? rejectedAccessToken = null,
        bool forUserSwitch = false,
        bool forceRefresh = false)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                // Empty RT means the session cannot be renewed (e.g. after InvalidGrant cleared it).
                // Do not treat as transient: solo restore would otherwise SignInOffline and skip
                // select-profile / reconnect badge on the next cold start.
                var storedWhenEmpty = ReadStoredSession();
                var mustHitWhenEmpty = forceRefresh
                    || (!string.IsNullOrEmpty(rejectedAccessToken)
                        && string.Equals(storedWhenEmpty.AccessToken, rejectedAccessToken, StringComparison.Ordinal));

                if (!forUserSwitch
                    && !mustHitWhenEmpty
                    && TryRestoreFromStoredSession(storedWhenEmpty))
                    return RefreshOutcome.Success;

                return RefreshOutcome.InvalidGrant;
            }

            var requestedRefreshToken = refreshToken;
            refreshToken = ResolveRefreshTokenForGrant(requestedRefreshToken, forUserSwitch);

            if (string.IsNullOrEmpty(refreshToken))
                return RefreshOutcome.InvalidGrant;

            var accessToken = _deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN);

            // Reuse a still-valid access token unless the caller just got a 401 for that exact token,
            // or proactive rotation was requested (native ExoPlayer keeps a baked Bearer).
            // Never reuse on user switch - SwitchToUserAsync clears the prior session first.
            // Validity comes from the stored expiry: older servers issue opaque JWEs.
            var mustHitTokenEndpoint = forceRefresh
                || (!string.IsNullOrEmpty(rejectedAccessToken)
                    && string.Equals(accessToken, rejectedAccessToken, StringComparison.Ordinal));

            if (!forUserSwitch
                && !mustHitTokenEndpoint
                && TryRestoreFromStoredSession(ReadStoredSession(accessToken)))
                return RefreshOutcome.Success;

            // Another waiter may already have redeemed this RT while we queued on the lock.
            // Never redeem the same RT twice in-process (OpenIddict rolling reuse revocation).
            if (!string.IsNullOrEmpty(_lastRedeemedRefreshToken)
                && string.Equals(refreshToken, _lastRedeemedRefreshToken, StringComparison.Ordinal))
            {
                var rotated = _deviceStorageService.Get(PreferenceKeys.REFRESH_TOKEN);
                if (!string.IsNullOrEmpty(rotated)
                    && !string.Equals(rotated, _lastRedeemedRefreshToken, StringComparison.Ordinal))
                {
                    refreshToken = rotated;
                }
                else if (!mustHitTokenEndpoint && TryRestoreFromStoredSession(ReadStoredSession(accessToken)))
                {
                    return RefreshOutcome.Success;
                }
                else
                {
                    return RefreshOutcome.TransientFailure;
                }
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                var redeemingRefreshToken = refreshToken;
                MauiStartupTrace.MarkRepeat("AuthRefresh-token-endpoint-start");
                // Thread pool: the first OpenIddict pipeline run JITs ~1400 methods and the
                // continuations would otherwise land on the caller's context (Blazor dispatcher
                // on the main thread for the solo auto-login), stalling first paint ~2 s on TV.
                // AsTask: the OpenIddict call returns a ValueTask, which Task.Run does not unwrap.
                var result = await Task.Run(() => OpenIddict.AuthenticateWithRefreshTokenAsync(new()
                {
                    CancellationToken = cts.Token,
                    RefreshToken = redeemingRefreshToken,
                    ProviderName = "K7"
                }).AsTask(), cts.Token);
                MauiStartupTrace.MarkRepeat("AuthRefresh-token-endpoint-end");

                _lastRedeemedRefreshToken = redeemingRefreshToken;

                _currentUser = new ClaimsPrincipal(new ClaimsIdentity(result.Principal.Claims, "OpenIddict", Claims.Name, Claims.Role));

                StoreAccessToken(result.AccessToken, result.AccessTokenExpirationDate);

                if (!string.IsNullOrEmpty(result.RefreshToken))
                    PersistRefreshToken(result.RefreshToken);

                if (!HasPersistedSessionTokens())
                    return RefreshOutcome.TransientFailure;

                // Refresh token flow may not include the name claim in the principal.
                if (_currentUser.Identity?.Name is null)
                {
                    try
                    {
                        var serverUser = await _userAdminService.GetCurrentUserAsync(cts.Token);
                        if (serverUser?.UserName is not null)
                        {
                            var claims = new List<Claim>(_currentUser.Claims)
                            {
                                new(Claims.Name, serverUser.UserName)
                            };
                            _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "OpenIddict", Claims.Name, Claims.Role));
                        }
                    }
                    catch { }
                }

                PersistSessionClaims();
                return RefreshOutcome.Success;
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (ProtocolException ex) when (
                ex.Error is Errors.InvalidGrant or Errors.InvalidToken)
            {
                return RefreshOutcome.InvalidGrant;
            }
            catch
            {
                return RefreshOutcome.TransientFailure;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<bool> TryRefreshAsync(
        CancellationToken cancellationToken = default,
        string? rejectedAccessToken = null,
        bool forceRefresh = false)
    {
        var refreshToken = _deviceStorageService.Get(PreferenceKeys.REFRESH_TOKEN);
        if (string.IsNullOrEmpty(refreshToken))
        {
            var last = _localUserService.GetLastActive();
            if (MauiSessionRestore.CanReuseLastRefreshToken(last, _localUserService))
                refreshToken = last!.RefreshToken;
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            // Online principal without a refresh token cannot recover - end session so the
            // guard sends the user to select-profile instead of a hollow authenticated shell.
            if (_currentUser.Identity?.IsAuthenticated == true
                && _currentUser.Identity.AuthenticationType != "Offline")
            {
                await EndSessionAsync(cancellationToken);
            }

            return false;
        }

        var outcome = await TryRefreshTokenAsync(
            refreshToken,
            cancellationToken,
            rejectedAccessToken,
            forceRefresh: forceRefresh);
        if (outcome == RefreshOutcome.Success)
        {
            await SaveLocalUserFromCurrentUserAsync(cancellationToken);
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            return true;
        }

        if (outcome == RefreshOutcome.InvalidGrant)
        {
            await EndSessionAsync(cancellationToken);
            return false;
        }

        return false;
    }

    public async Task<bool> SwitchToUserAsync(string identityUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identityUserId))
            return false;

        // Always re-read from storage - the in-memory SelectProfile list can hold a stale RT
        // after a background rotation of another (or the same) session.
        var localUser = _localUserService.GetAll()
            .FirstOrDefault(u => string.Equals(u.IdentityUserId, identityUserId, StringComparison.Ordinal));
        if (localUser is null || string.IsNullOrEmpty(localUser.RefreshToken))
            return false;

        // SecureStorage only holds the *active* session. Drop it before redeeming another
        // profile's refresh token, otherwise a still-valid access token / RT from the previous
        // user would keep us signed in as that previous user.
        ClearActiveSession();

        var outcome = await TryRefreshTokenAsync(localUser.RefreshToken, cancellationToken, forUserSwitch: true);
        if (outcome != RefreshOutcome.Success)
        {
            if (outcome == RefreshOutcome.InvalidGrant)
                _localUserService.ClearRefreshToken(identityUserId);
            return false;
        }

        if (!string.Equals(GetCurrentIdentityUserId(), identityUserId, StringComparison.Ordinal))
        {
            ClearActiveSession();
            return false;
        }

        await SaveLocalUserFromCurrentUserAsync(cancellationToken);
        await TryAttachCurrentUserToDeviceAsync(cancellationToken);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return true;
    }

    private void ClearActiveSession()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;
        ClearStoredTokens();
    }

    private async Task SaveLocalUserFromCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var identityUserId = GetCurrentIdentityUserId();
        if (string.IsNullOrEmpty(identityUserId))
            return;

        var existing = _localUserService.GetAll().FirstOrDefault(u => u.IdentityUserId == identityUserId);
        var currentRefreshToken = _deviceStorageService.Get(PreferenceKeys.REFRESH_TOKEN);
        if (string.IsNullOrEmpty(currentRefreshToken))
            currentRefreshToken = existing?.RefreshToken;

        if (string.IsNullOrEmpty(currentRefreshToken))
            return;

        var localUser = new LocalUser
        {
            IdentityUserId = identityUserId,
            UserName = _currentUser.FindFirst(ClaimTypes.Name)?.Value
                       ?? _currentUser.FindFirst("preferred_username")?.Value
                       ?? _currentUser.FindFirst("name")?.Value
                       ?? existing?.UserName
                       ?? "User",
            Email = _currentUser.FindFirst(ClaimTypes.Email)?.Value
                    ?? _currentUser.FindFirst("email")?.Value
                    ?? existing?.Email,
            RefreshToken = currentRefreshToken,
            AvatarUrl = existing?.AvatarUrl,
            DisplayName = existing?.DisplayName,
            UserId = existing?.UserId,
            HasPin = existing?.HasPin ?? false
        };

        try
        {
            var serverUser = await _userAdminService.GetCurrentUserAsync(cancellationToken);
            if (serverUser is not null)
            {
                localUser.UserId = serverUser.Id;
                localUser.HasPin = serverUser.HasPin;
                if (serverUser.UserName is not null)
                    localUser.UserName = serverUser.UserName;
                if (serverUser.Email is not null)
                    localUser.Email = serverUser.Email;
                if (serverUser.DisplayName is not null)
                    localUser.DisplayName = serverUser.DisplayName;
                localUser.AvatarUrl = serverUser.AvatarUrl;
            }
        }
        catch { }

        _localUserService.SaveOrUpdate(localUser);
    }

    /// <summary>
    /// For same-session refresh, always prefer SecureStorage (may already hold a rotated RT).
    /// For user switch, only prefer SecureStorage when it belongs to the same LocalUser
    /// (e.g. a prior rotation that was not yet mirrored into LocalUser.RefreshToken).
    /// </summary>
    private string ResolveRefreshTokenForGrant(string requestedRefreshToken, bool forUserSwitch)
    {
        var storedRefreshToken = _deviceStorageService.Get(PreferenceKeys.REFRESH_TOKEN);
        if (string.IsNullOrEmpty(storedRefreshToken))
            return requestedRefreshToken;

        if (!forUserSwitch)
            return storedRefreshToken;

        if (string.Equals(storedRefreshToken, requestedRefreshToken, StringComparison.Ordinal))
            return storedRefreshToken;

        var targetIdentity = FindIdentityUserIdForRefreshToken(requestedRefreshToken);
        if (string.IsNullOrEmpty(targetIdentity))
            return requestedRefreshToken;

        var accessSubject = TryReadSubject(_deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN));
        if (string.Equals(accessSubject, targetIdentity, StringComparison.Ordinal))
            return storedRefreshToken;

        if (_localUserService.GetAll().Any(u =>
                u.IdentityUserId == targetIdentity
                && string.Equals(u.RefreshToken, storedRefreshToken, StringComparison.Ordinal)))
            return storedRefreshToken;

        return requestedRefreshToken;
    }

    private string? FindIdentityUserIdForRefreshToken(string refreshToken) =>
        _localUserService.GetAll()
            .FirstOrDefault(u => string.Equals(u.RefreshToken, refreshToken, StringComparison.Ordinal))
            ?.IdentityUserId;

    private string? GetCurrentIdentityUserId() =>
        _currentUser.FindFirst(Claims.Subject)?.Value
        ?? _currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private void PersistRefreshToken(string refreshToken)
    {
        _deviceStorageService.Set(PreferenceKeys.REFRESH_TOKEN, refreshToken);

        var identityUserId = GetCurrentIdentityUserId();
        if (!string.IsNullOrEmpty(identityUserId))
            _localUserService.UpdateRefreshToken(identityUserId, refreshToken);
    }

    private string? TryReadSubject(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
            return null;

        // Encrypted token: fall back to the claims snapshot stored with it.
        var claims = JwtAccessToken.ReadClaims(accessToken);
        if (claims.Count == 0)
            claims = StoredSession.DeserializeClaims(_deviceStorageService.Get(PreferenceKeys.SESSION_CLAIMS));

        return claims.FirstOrDefault(c => c.Type == Claims.Subject)?.Value
               ?? claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
    }

    private StoredSessionSnapshot ReadStoredSession() =>
        ReadStoredSession(_deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN));

    private StoredSessionSnapshot ReadStoredSession(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
            return new StoredSessionSnapshot(null, null, null);

        return new StoredSessionSnapshot(
            accessToken,
            StoredSession.ParseExpiry(_deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN_EXPIRES_AT)),
            _deviceStorageService.Get(PreferenceKeys.SESSION_CLAIMS));
    }

    /// <summary>
    /// Signs in from storage without the token endpoint: the stored access token is still
    /// valid (expiry taken from the token response - older servers issue opaque JWEs) and the claims
    /// snapshot carries a user id. Applies the Bearer without rewriting storage.
    /// </summary>
    private bool TryRestoreFromStoredSession(StoredSessionSnapshot stored)
    {
        if (string.IsNullOrEmpty(stored.AccessToken))
            return false;

        var expiresAt = ResolveAccessTokenExpiry(stored.AccessToken, stored.ExpiresAt);
        if (!StoredSession.IsAccessTokenUsable(expiresAt, DateTimeOffset.UtcNow))
            return false;

        var claims = JwtAccessToken.ReadClaims(stored.AccessToken);
        if (claims.Count == 0)
            claims = StoredSession.DeserializeClaims(stored.ClaimsJson);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "OpenIddict", Claims.Name, Claims.Role));
        if (AuthIdentity.GetUserId(principal) is null)
            return false;

        _currentUser = principal;
        _accessTokenExpiresAt = expiresAt;
        ApplyAccessToken(stored.AccessToken);
        MauiStartupTrace.MarkRepeat("AuthSession-restored-from-storage");
        return true;
    }

    private static DateTimeOffset? ResolveAccessTokenExpiry(string? accessToken, DateTimeOffset? storedExpiry) =>
        storedExpiry ?? JwtAccessToken.TryReadExpiry(accessToken);

    private bool IsStoredAccessTokenUsable()
    {
        var accessToken = _deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN);
        if (string.IsNullOrEmpty(accessToken))
            return false;

        var expiresAt = _accessTokenExpiresAt ?? ResolveAccessTokenExpiry(
            accessToken,
            StoredSession.ParseExpiry(_deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN_EXPIRES_AT)));
        return StoredSession.IsAccessTokenUsable(expiresAt, DateTimeOffset.UtcNow);
    }

    private void ClearStoredTokens()
    {
        RemoveStoredAccessToken();
        _deviceStorageService.Remove(PreferenceKeys.REFRESH_TOKEN);
    }

    private void RemoveStoredAccessToken()
    {
        _accessTokenExpiresAt = null;
        _deviceStorageService.Remove(PreferenceKeys.ACCESS_TOKEN);
        _deviceStorageService.Remove(PreferenceKeys.ACCESS_TOKEN_EXPIRES_AT);
        _deviceStorageService.Remove(PreferenceKeys.SESSION_CLAIMS);
    }

    private static string? ResolveInteractiveAccessToken(OpenIddictClientModels.InteractiveAuthenticationResult result) =>
        result.BackchannelAccessToken ?? result.FrontchannelAccessToken;

    private static DateTimeOffset? ResolveInteractiveAccessTokenExpiry(OpenIddictClientModels.InteractiveAuthenticationResult result) =>
        result.BackchannelAccessToken is not null
            ? result.BackchannelAccessTokenExpirationDate
            : result.FrontchannelAccessTokenExpirationDate;

    /// <summary>
    /// Persists the token with its expiry from the token response. K7 access tokens are
    /// encrypted, so this stored date is the only way to know when the token stops working.
    /// </summary>
    private void StoreAccessToken(string? accessToken, DateTimeOffset? expiresAt)
    {
        if (string.IsNullOrEmpty(accessToken))
            return;

        var previous = _deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN);

        ApplyAccessToken(accessToken);
        _deviceStorageService.Set(PreferenceKeys.ACCESS_TOKEN, accessToken);
        PersistAccessTokenExpiry(expiresAt);

        if (!string.Equals(previous, accessToken, StringComparison.Ordinal))
        {
            MauiStartupTrace.MarkRepeat("AccessToken-changed");
            AccessTokenChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyAccessToken(string accessToken)
    {
        _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        _hubClient?.UpdateAccessToken(accessToken);
#if WINDOWS
        WindowsStreamAuthContext.UpdateFrom(_k7ServerService);
#endif
    }

    private void PersistAccessTokenExpiry(DateTimeOffset? expiresAt)
    {
        _accessTokenExpiresAt = expiresAt;
        if (expiresAt is { } value)
            _deviceStorageService.Set(PreferenceKeys.ACCESS_TOKEN_EXPIRES_AT, StoredSession.FormatExpiry(value));
        else
            _deviceStorageService.Remove(PreferenceKeys.ACCESS_TOKEN_EXPIRES_AT);
    }

    /// <summary>
    /// Snapshot of the signed-in principal (id, name, roles) for the storage restore path.
    /// Removed with the access token.
    /// </summary>
    private void PersistSessionClaims()
    {
        if (!AuthIdentity.IsOnlineAuthenticated(_currentUser))
            return;

        _deviceStorageService.Set(PreferenceKeys.SESSION_CLAIMS, StoredSession.SerializeClaims(_currentUser.Claims));
    }

    private bool HasPersistedSessionTokens()
    {
        if (!string.IsNullOrEmpty(_deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN)))
            return true;

        return !string.IsNullOrEmpty(_deviceStorageService.Get(PreferenceKeys.REFRESH_TOKEN));
    }

    private bool HasUsableOnlineAccessToken() =>
        AuthIdentity.IsOnlineAuthenticated(_currentUser) && IsStoredAccessTokenUsable();

    private void AllowRestoreRetryIfNeeded()
    {
        var restore = _restoreTask;
        var restoreInFlight = restore is not null && !restore.IsCompleted;
        if (!MauiSessionRestore.ShouldRetryHeadlessRestore(
                serverConfigured: _k7ServerService.HttpClient.BaseAddress is not null,
                restoreAlreadyAttempted: Volatile.Read(ref _initialized) != 0,
                restoreInFlight: restoreInFlight,
                hasUsableOnlineAccessToken: HasUsableOnlineAccessToken()))
            return;

        lock (_initLock)
        {
            restore = _restoreTask;
            restoreInFlight = restore is not null && !restore.IsCompleted;
            if (!MauiSessionRestore.ShouldRetryHeadlessRestore(
                    serverConfigured: _k7ServerService.HttpClient.BaseAddress is not null,
                    restoreAlreadyAttempted: Volatile.Read(ref _initialized) != 0,
                    restoreInFlight: restoreInFlight,
                    hasUsableOnlineAccessToken: HasUsableOnlineAccessToken()))
                return;

            Volatile.Write(ref _initialized, 0);
            _restoreTask = null;
        }
    }

    private static async Task WaitForNetworkHintAsync(CancellationToken cancellationToken)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.None)
            return;

        var deadline = DateTime.UtcNow.Add(MauiSessionRestore.HeadlessNetworkWait);
        while (DateTime.UtcNow < deadline && Connectivity.Current.NetworkAccess == NetworkAccess.None)
            await Task.Delay(200, cancellationToken);
    }

    private async Task TryAttachCurrentUserToDeviceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var storedDeviceId = _deviceStorageService.Get(PreferenceKeys.DEVICE_ID);
            if (Guid.TryParse(storedDeviceId, out var parsedId))
            {
                await _deviceApiService.AttachCurrentUserToDeviceAsync(parsedId, cancellationToken);
            }
        }
        catch { }
    }

    private async Task RestoreSharedProfileAsync()
    {
        if (_viewingGroupSession is null || _viewingGroupCache is null)
            return;

        try
        {
            await _viewingGroupCache.RefreshAsync();
        }
        catch { }

        await _viewingGroupSession.RestoreLastActiveAsync();
    }
}

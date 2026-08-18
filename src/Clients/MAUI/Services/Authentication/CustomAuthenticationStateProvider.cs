using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Shared;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
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

    private readonly OpenIddictClientService _openIddictClientService;
    private readonly IK7ServerService _k7ServerService;
    private readonly IUserAdminService _userAdminService;
    private readonly IDeviceApiService _deviceApiService;
    private readonly IDeviceStorageService _deviceStorageService;
    private readonly ILocalUserService _localUserService;
    private readonly ISharedProfileSessionService? _viewingGroupSession;
    private readonly ISharedProfileLocalCache? _viewingGroupCache;
    private readonly K7HubClient? _hubClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private int _initialized; // 0 = not started, 1 = started
    private Task? _restoreTask;
    private readonly object _initLock = new();
    private static readonly AsyncLocal<bool> RestoreOnCallStack = new();
    /// <summary>
    /// Last refresh token successfully redeemed in this process. Redeeming it again triggers
    /// OpenIddict rolling-token reuse detection and revokes the whole authorization family.
    /// </summary>
    private string? _lastRedeemedRefreshToken;

    /// <summary>
    /// True while cold-start restore is running on this async flow. The auth HTTP handler
    /// must not await <see cref="GetAuthenticationStateAsync"/> in that case or it deadlocks
    /// on nested calls like /api/users/me during token enrichment.
    /// </summary>
    internal bool IsSessionRestoreInProgress => RestoreOnCallStack.Value;

    public CustomAuthenticationStateProvider(
        OpenIddictClientService openIddictClientService,
        IK7ServerService k7ServerService,
        IUserAdminService userAdminService,
        IDeviceApiService deviceApiService,
        IDeviceStorageService deviceStorageService,
        ILocalUserService localUserService,
        ISharedProfileSessionService? viewingGroupSession = null,
        ISharedProfileLocalCache? viewingGroupCache = null,
        K7HubClient? hubClient = null)
    {
        _openIddictClientService = openIddictClientService;
        _k7ServerService = k7ServerService;
        _userAdminService = userAdminService;
        _deviceApiService = deviceApiService;
        _deviceStorageService = deviceStorageService;
        _localUserService = localUserService;
        _viewingGroupSession = viewingGroupSession;
        _viewingGroupCache = viewingGroupCache;
        _hubClient = hubClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (Volatile.Read(ref _initialized) == 0)
        {
            lock (_initLock)
            {
                if (_initialized == 0)
                {
                    _restoreTask = TryRestoreSessionAsync();
                    Volatile.Write(ref _initialized, 1);
                }
            }
        }

        if (_restoreTask is not null)
            await _restoreTask;

        return new AuthenticationState(_currentUser);
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));

        try
        {
            // Run interactive auth off the Blazor sync context so the WebView UI
            // stays responsive while the system browser completes the redirect.
            await Task.Run(async () =>
            {
                var challenge = await _openIddictClientService.ChallengeInteractivelyAsync(new()
                {
                    CancellationToken = timeout.Token,
                    ProviderName = "K7",
                    AdditionalAuthorizationRequestParameters = new Dictionary<string, OpenIddict.Abstractions.OpenIddictParameter>
                    {
                        ["prompt"] = "login"
                    }
                }).ConfigureAwait(false);

                var result = await _openIddictClientService.AuthenticateInteractivelyAsync(new()
                {
                    CancellationToken = timeout.Token,
                    Nonce = challenge.Nonce
                }).ConfigureAwait(false);

                _currentUser = new ClaimsPrincipal(new ClaimsIdentity(result.Principal.Claims, "OpenIddict", Claims.Name, Claims.Role));

                var accessToken = ResolveInteractiveAccessToken(result);
                StoreAccessToken(accessToken);

                if (!string.IsNullOrEmpty(result.RefreshToken))
                    PersistRefreshToken(result.RefreshToken);

                if (!HasPersistedSessionTokens())
                {
                    _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                    ClearStoredTokens();
                    _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;
                }
                else
                {
                    await SaveLocalUserFromCurrentUserAsync(timeout.Token).ConfigureAwait(false);
                    await TryAttachCurrentUserToDeviceAsync(timeout.Token).ConfigureAwait(false);
                }
            }, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
        catch (ProtocolException exception) when (exception.Error is Errors.AccessDenied)
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
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
                StoreAccessToken(accessToken);

                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(accessToken))
                {
                    var jwt = handler.ReadJwtToken(accessToken);
                    _currentUser = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "OpenIddict", Claims.Name, Claims.Role));
                }
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
        var challenge = await _openIddictClientService.ChallengeUsingDeviceAsync(new()
        {
            CancellationToken = cancellationToken,
            ProviderName = "K7"
        });

        await onDeviceCodeReceived(new DeviceCodeInfo(
            challenge.UserCode,
            challenge.VerificationUri.ToString(),
            challenge.VerificationUriComplete?.ToString() ?? challenge.VerificationUri + "?user_code=" + Uri.EscapeDataString(challenge.UserCode),
            DateTimeOffset.UtcNow.Add(challenge.ExpiresIn)));

        var result = await _openIddictClientService.AuthenticateWithDeviceAsync(new()
        {
            CancellationToken = cancellationToken,
            DeviceCode = challenge.DeviceCode,
            Interval = challenge.Interval,
            Timeout = challenge.ExpiresIn,
            ProviderName = "K7"
        });

        _currentUser = new ClaimsPrincipal(new ClaimsIdentity(result.Principal.Claims, "OpenIddict", Claims.Name, Claims.Role));

        StoreAccessToken(result.AccessToken);

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
        _deviceStorageService.Remove(PreferenceKeys.ACCESS_TOKEN);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private async Task TryRestoreSessionAsync()
    {
        RestoreOnCallStack.Value = true;
        try
        {
            await TryRestoreSessionCoreAsync();
        }
        finally
        {
            RestoreOnCallStack.Value = false;
        }
    }

    private async Task TryRestoreSessionCoreAsync()
    {
        if (!_localUserService.IsSingleUserMode)
            return;

        var lastUser = ResolveSingleUserModeRestoreTarget();
        if (lastUser is null)
            return;

        // Only skip online restore when there is truly no network.
        // NetworkAccess.Local / ConstrainedInternet are common for self-hosted LAN
        // servers and Android Auto (phone may have no upstream Internet). Treating
        // those as offline wiped the access token and left AA online tabs empty.
        if (Connectivity.Current.NetworkAccess == NetworkAccess.None)
        {
            SignInOffline(lastUser);
            await RestoreSharedProfileAsync();
            return;
        }

        try
        {
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

        ClearStoredTokens();
        SignInOffline(lastUser);
    }

    /// <summary>
    /// Solo restore only when the preference is on and a profile was successfully entered
    /// while solo was enabled (LastActive + unlock). Checking the box alone is not enough.
    /// </summary>
    private LocalUser? ResolveSingleUserModeRestoreTarget()
    {
        var lastUser = _localUserService.GetLastActive();
        if (lastUser is null)
            return null;

        if (!_localUserService.IsSingleUserUnlocked(lastUser.IdentityUserId))
            return null;

        return lastUser;
    }

    private async Task RestoreUserInBackgroundAsync(LocalUser localUser)
    {
        var refreshToken = _deviceStorageService.Get(PreferenceKeys.REFRESH_TOKEN) ?? localUser.RefreshToken;

        var outcome = await TryRefreshTokenAsync(refreshToken);
        if (outcome == RefreshOutcome.Success)
        {
            await SaveLocalUserFromCurrentUserAsync();
            await RestoreSharedProfileAsync();
            NotifyAuthenticationStateChanged(
                Task.FromResult(new AuthenticationState(_currentUser)));
            return;
        }

        // Invalid grant / revoked family: stay anonymous so AuthorizeRouteView redirects to
        // select-profile. Signing in offline would satisfy [Authorize] with an empty online UI.
        if (outcome == RefreshOutcome.InvalidGrant)
        {
            ClearStoredTokens();
            if (!string.IsNullOrEmpty(localUser.IdentityUserId))
                _localUserService.ClearRefreshToken(localUser.IdentityUserId);
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(
                Task.FromResult(new AuthenticationState(_currentUser)));
            return;
        }

        // Transient failure with network hiccups: keep an offline session for local media / AA.
        ClearStoredTokens();
        SignInOffline(localUser);
        await RestoreSharedProfileAsync();
    }

    private enum RefreshOutcome
    {
        Success,
        TransientFailure,
        InvalidGrant
    }

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
                var accessTokenWhenEmpty = _deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN);
                var mustHitWhenEmpty = forceRefresh
                    || (!string.IsNullOrEmpty(rejectedAccessToken)
                        && string.Equals(accessTokenWhenEmpty, rejectedAccessToken, StringComparison.Ordinal));

                if (!forUserSwitch
                    && !mustHitWhenEmpty
                    && TryRestoreFromAccessToken(accessTokenWhenEmpty))
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
            var mustHitTokenEndpoint = forceRefresh
                || (!string.IsNullOrEmpty(rejectedAccessToken)
                    && string.Equals(accessToken, rejectedAccessToken, StringComparison.Ordinal));

            if (!forUserSwitch
                && !mustHitTokenEndpoint
                && TryRestoreFromAccessToken(accessToken))
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
                else if (!mustHitTokenEndpoint && TryRestoreFromAccessToken(accessToken))
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
                var result = await _openIddictClientService.AuthenticateWithRefreshTokenAsync(new()
                {
                    CancellationToken = cts.Token,
                    RefreshToken = redeemingRefreshToken,
                    ProviderName = "K7"
                });

                _lastRedeemedRefreshToken = redeemingRefreshToken;

                _currentUser = new ClaimsPrincipal(new ClaimsIdentity(result.Principal.Claims, "OpenIddict", Claims.Name, Claims.Role));

                StoreAccessToken(result.AccessToken);

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

    private static string? TryReadSubject(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
            return null;

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
            return null;

        var jwt = handler.ReadJwtToken(accessToken);
        return jwt.Subject
               ?? jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
    }

    private static bool IsAccessTokenValid(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
            return false;

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
            return false;

        var jwt = handler.ReadJwtToken(accessToken);
        return jwt.ValidTo > DateTime.UtcNow.AddMinutes(1);
    }

    private bool TryRestoreFromAccessToken(string? accessToken)
    {
        if (!IsAccessTokenValid(accessToken))
            return false;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(accessToken!);
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "OpenIddict", Claims.Name, Claims.Role));
        StoreAccessToken(accessToken);
        return true;
    }

    private void ClearStoredTokens()
    {
        _deviceStorageService.Remove(PreferenceKeys.ACCESS_TOKEN);
        _deviceStorageService.Remove(PreferenceKeys.REFRESH_TOKEN);
    }

    private static string? ResolveInteractiveAccessToken(OpenIddictClientModels.InteractiveAuthenticationResult result) =>
        result.BackchannelAccessToken ?? result.FrontchannelAccessToken;

    private void StoreAccessToken(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
            return;

        var previous = _deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN);

        _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        _deviceStorageService.Set(PreferenceKeys.ACCESS_TOKEN, accessToken);
        _hubClient?.UpdateAccessToken(accessToken);
#if WINDOWS
        WindowsStreamAuthContext.UpdateFrom(_k7ServerService);
#endif

        if (!string.Equals(previous, accessToken, StringComparison.Ordinal))
            AccessTokenChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool HasPersistedSessionTokens()
    {
        if (!string.IsNullOrEmpty(_deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN)))
            return true;

        return !string.IsNullOrEmpty(_deviceStorageService.Get(PreferenceKeys.REFRESH_TOKEN));
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

using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class DeviceActivation : IDisposable
{
    [Inject] private ICustomAuthenticationStateProvider AuthService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private ILocalUserService LocalUserService { get; set; } = default!;
    [Inject] private ISharedProfileDevicePinService SharedProfileDevicePin { get; set; } = default!;
    [Inject] private ISharedProfileLocalCache SharedProfileCache { get; set; } = default!;
    [Inject] private IDeviceStorageService Storage { get; set; } = default!;

    private bool _loading = true;
    private DeviceCodeInfo? _deviceCode;
    private string? _displayUri;
    private bool _authorized;
    private bool _canGoBack;
    private bool _disposed;

    private CancellationTokenSource? _cts;
    private PeriodicTimer? _timer;
    private int _remainingSeconds;
    private int _totalSeconds;
    private int _flowGeneration;

    private double _progress => _totalSeconds > 0
        ? (double)_remainingSeconds / _totalSeconds * 100
        : 0;

    protected override void OnInitialized()
    {
        _canGoBack = LocalUserService.GetAll().Count > 0;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            _ = StartDeviceCodeFlowAsync();
    }

    private async Task StartDeviceCodeFlowAsync()
    {
        var generation = Interlocked.Increment(ref _flowGeneration);
        _loading = true;
        _deviceCode = null;
        _authorized = false;
        StopTimer();
        CancelAndReplaceCts();
        var ct = _cts!.Token;
        await InvokeAsync(StateHasChanged);

        var receivedCode = false;

        try
        {
            await AuthService.LoginWithDeviceCodeAsync(async info =>
            {
                if (!IsCurrentFlow(generation))
                    return;

                receivedCode = true;
                _deviceCode = info;
                _displayUri = info.VerificationUri.Replace("/connect/verify", "/link");
                _loading = false;

                _totalSeconds = Math.Max(1, (int)(info.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds);
                _remainingSeconds = _totalSeconds;

                await InvokeAsync(StateHasChanged);

                await Task.Delay(100, ct);
                if (!IsCurrentFlow(generation))
                    return;

                await JSRuntime.InvokeVoidAsync("k7QrCode.generate", "device-qr", info.VerificationUriComplete, 400);

                StartCountdown(generation);
            }, ct);

            if (!IsCurrentFlow(generation))
                return;

            StopTimer();
            _authorized = true;
            Snackbar.Add(L["ActivationSuccess"], K7Severity.Success);
            await InvokeAsync(StateHasChanged);

            await Task.Delay(1500);
            if (!IsCurrentFlow(generation))
                return;

            var cards = ProfilePickerHiddenUsers.VisibleCardCount(
                LocalUserService, SharedProfileDevicePin, SharedProfileCache, Storage);
            var destination = cards > 1 ? "/select-profile" : "/";
            await InvokeAsync(() => Navigation.NavigateTo(destination));
        }
        catch (OperationCanceledException) when (!IsCurrentFlow(generation))
        {
            // Superseded by a newer flow or dispose.
        }
        catch (Exception ex) when (IsCurrentFlow(generation) && receivedCode && IsRecoverableDeviceCodeFailure(ex))
        {
            // OpenIddict timeout / expired_token after a code was shown: refresh QR.
            await InvokeAsync(() => _ = StartDeviceCodeFlowAsync());
        }
        catch (Exception ex) when (IsCurrentFlow(generation))
        {
            StopTimer();
            Snackbar.Add(ex.Message, K7Severity.Error);
            _loading = false;
            _deviceCode = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void StartCountdown(int generation)
    {
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _ = TickAsync(generation, _cts?.Token ?? CancellationToken.None);
    }

    private async Task TickAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync(cancellationToken))
            {
                if (!IsCurrentFlow(generation))
                    return;

                _remainingSeconds--;

                if (_remainingSeconds <= 0)
                {
                    await InvokeAsync(() =>
                    {
                        if (IsCurrentFlow(generation))
                            _ = StartDeviceCodeFlowAsync();
                    });
                    return;
                }

                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void CancelAndReplaceCts()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
    }

    private bool IsCurrentFlow(int generation) =>
        !_disposed && generation == _flowGeneration;

    private static bool IsRecoverableDeviceCodeFailure(Exception ex)
    {
        if (ex is TimeoutException or OperationCanceledException or TaskCanceledException)
            return true;

        for (var current = ex; current is not null; current = current.InnerException!)
        {
            var message = current.Message;
            if (message.Contains("expired", StringComparison.OrdinalIgnoreCase)
                || message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || message.Contains("expired_token", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private string FormatTime()
    {
        var minutes = _remainingSeconds / 60;
        var seconds = _remainingSeconds % 60;
        return $"{minutes}:{seconds:D2}";
    }

    private void GoBackToProfiles() => Navigation.NavigateTo("/select-profile");

    public void Dispose()
    {
        _disposed = true;
        Interlocked.Increment(ref _flowGeneration);
        StopTimer();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}

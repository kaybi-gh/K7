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

    private bool _loading = true;
    private DeviceCodeInfo? _deviceCode;
    private string? _displayUri;
    private bool _authorized;

    private CancellationTokenSource? _cts;
    private PeriodicTimer? _timer;
    private int _remainingSeconds;
    private int _totalSeconds;

    private double _progress => _totalSeconds > 0
        ? (double)_remainingSeconds / _totalSeconds * 100
        : 0;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            _ = StartDeviceCodeFlowAsync();
        }
    }

    private async Task StartDeviceCodeFlowAsync()
    {
        _loading = true;
        _deviceCode = null;
        _authorized = false;
        StopTimer();
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        StateHasChanged();

        try
        {
            await AuthService.LoginWithDeviceCodeAsync(async info =>
            {
                _deviceCode = info;
                _displayUri = info.VerificationUri.Replace("/connect/verify", "/link-device/authorize");
                _loading = false;

                _totalSeconds = Math.Max(1, (int)(info.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds);
                _remainingSeconds = _totalSeconds;

                await InvokeAsync(StateHasChanged);

                await Task.Delay(100);
                await JSRuntime.InvokeVoidAsync("k7QrCode.generate", "device-qr", info.VerificationUriComplete, 400);

                StartCountdown();
            }, _cts.Token);

            StopTimer();
            _authorized = true;
            Snackbar.Add(L["ActivationSuccess"], K7Severity.Success);
            await InvokeAsync(StateHasChanged);

            await Task.Delay(1500);
            await InvokeAsync(() => Navigation.NavigateTo("/"));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StopTimer();
            Snackbar.Add(ex.Message, K7Severity.Error);
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void StartCountdown()
    {
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _ = TickAsync(_cts?.Token ?? CancellationToken.None);
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync(cancellationToken))
            {
                _remainingSeconds--;

                if (_remainingSeconds <= 0)
                {
                    await InvokeAsync(() => _ = StartDeviceCodeFlowAsync());
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

    private string FormatTime()
    {
        var minutes = _remainingSeconds / 60;
        var seconds = _remainingSeconds % 60;
        return $"{minutes}:{seconds:D2}";
    }

    public void Dispose()
    {
        StopTimer();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

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
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        StateHasChanged();

        try
        {
            await AuthService.LoginWithDeviceCodeAsync(async info =>
            {
                _deviceCode = info;
                _displayUri = info.VerificationUri.Replace("/connect/verify", "/link");
                _loading = false;
                await InvokeAsync(StateHasChanged);

                await Task.Delay(100);
                await JSRuntime.InvokeVoidAsync("k7QrCode.generate", "device-qr", info.VerificationUriComplete);
            }, _cts.Token);

            _authorized = true;
            Snackbar.Add(L["ActivationSuccess"], K7Severity.Success);
            await InvokeAsync(StateHasChanged);

            await Task.Delay(1500);
            await InvokeAsync(() => Navigation.NavigateTo("/"));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

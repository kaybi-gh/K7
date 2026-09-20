using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class PlaybackHandoverListener : IDisposable
{
    private const string ResumeChoice = "resume";
    private RemotePlaybackHandler? _handler;
    private bool _dialogOpen;

    protected override void OnInitialized()
    {
        _handler = Services.GetService<RemotePlaybackHandler>();
        if (_handler is not null)
            _handler.HandoverChanged += OnHandoverChanged;
    }

    private void OnHandoverChanged()
    {
        if (_handler is null || !_handler.HasPendingHandover || _dialogOpen)
            return;

        _ = InvokeAsync(ShowHandoverDialogAsync);
    }

    private async Task ShowHandoverDialogAsync()
    {
        if (_handler is null || !_handler.HasPendingHandover)
            return;

        _dialogOpen = true;
        try
        {
            await Task.Delay(400);

            var dto = _handler.PendingHandover;
            var device = string.IsNullOrWhiteSpace(dto?.NewDeviceName) ? "Device" : dto.NewDeviceName;
            var parameters = new K7DialogParameters
            {
                ["Message"] = string.Format(S["PlaybackMovedMessage"].Value, device),
                ["YesText"] = S["PlaybackMovedOk"].Value,
                ["NoText"] = S["PlaybackMovedRemote"].Value,
                ["CancelText"] = S["PlaybackMovedResume"].Value,
                ["CancelResult"] = ResumeChoice
            };
            var options = new K7DialogOptions
            {
                MaxWidth = K7DialogMaxWidth.ExtraSmall,
                FullWidth = true
            };
            var dialog = await DialogService.ShowAsync<K7MessageBoxDialog>(
                S["PlaybackMovedTitle"].Value,
                parameters,
                options);
            var result = await dialog.Result;

            if (result is null || result.Canceled || result.Data is true)
                await _handler.ConfirmHandoverDismissAsync();
            else if (result.Data is false)
                await _handler.ConfirmHandoverRemoteAsync();
            else if (result.Data is ResumeChoice)
                await _handler.ConfirmHandoverResumeAsync();
            else
                await _handler.ConfirmHandoverDismissAsync();
        }
        catch
        {
            if (_handler is not null)
                await _handler.ConfirmHandoverDismissAsync();
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    public void Dispose()
    {
        if (_handler is not null)
            _handler.HandoverChanged -= OnHandoverChanged;
    }
}

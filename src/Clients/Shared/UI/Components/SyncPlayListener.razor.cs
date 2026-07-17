using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class SyncPlayListener : IDisposable
{
    protected override void OnInitialized()
    {
        SyncPlay.InvitationReceived += OnInvitationReceived;
        SyncPlay.InviteLinkReceived += OnInviteLinkReceived;
    }

    private void OnInvitationReceived(SyncPlayInvitationDto invitation)
    {
        _ = InvokeAsync(async () =>
        {
            var parameters = new K7DialogParameters<SyncPlayInvitationDialog>();
            parameters.Add(d => d.Invitation, invitation);

            var reference = await DialogService.ShowAsync<SyncPlayInvitationDialog>("SyncPlay Invitation", parameters);
            var result = await reference.Result;

            if (!result.Canceled)
            {
                await DialogService.ShowAsync<SyncPlayDialog>("SyncPlay", options: new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true });
            }
        });
    }

    private void OnInviteLinkReceived(SyncPlayInviteLinkDto link)
    {
        _ = InvokeAsync(async () =>
        {
            var url = $"{Navigation.BaseUri}syncplay/join/{link.Token}";
            await JS.InvokeVoidAsync("K7.shareOrCopy", url);
        });
    }

    public void Dispose()
    {
        SyncPlay.InvitationReceived -= OnInvitationReceived;
        SyncPlay.InviteLinkReceived -= OnInviteLinkReceived;
    }
}

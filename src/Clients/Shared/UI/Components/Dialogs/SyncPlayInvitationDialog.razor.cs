using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class SyncPlayInvitationDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public SyncPlayInvitationDto Invitation { get; set; } = default!;

    [Inject] private ILocalUserService LocalUserService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _loading;
    private bool _failed;
    private bool _isGuest;
    private string _guestNickname = "";

    protected override void OnInitialized()
    {
        _isGuest = LocalUserService.GetLastActive() is null;
    }

    private string GetInitial() => Invitation.InviterDisplayName.Length > 0
        ? Invitation.InviterDisplayName[..1].ToUpperInvariant()
        : "?";

    private void Decline() => Dialog.Cancel();

    private async Task Accept()
    {
        _loading = true;
        _failed = false;

        try
        {
            await JS.InvokeVoidAsync("K7.unlockAudio");
            var displayName = _isGuest && !string.IsNullOrWhiteSpace(_guestNickname)
                ? _guestNickname.Trim()
                : null;
            await SyncPlay.JoinGroupAsync(Invitation.GroupId, guestDisplayName: displayName);
            Dialog.Close(K7DialogResult.Ok());
        }
        catch
        {
            _failed = true;
        }
        finally
        {
            _loading = false;
        }
    }
}

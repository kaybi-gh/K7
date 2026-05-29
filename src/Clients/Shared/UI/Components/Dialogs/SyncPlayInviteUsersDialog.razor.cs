using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class SyncPlayInviteUsersDialog : IDisposable
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    private readonly HashSet<string> _invitedUsers = [];
    private readonly HashSet<string> _failedUsers = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        SyncPlay.OnlineUsersUpdated += OnOnlineUsersUpdated;
        await SyncPlay.RefreshOnlineUsersAsync();
        _loading = false;
    }

    private async Task InviteUser(string userId)
    {
        _failedUsers.Remove(userId);
        _invitedUsers.Add(userId);

        try
        {
            await SyncPlay.InviteUserAsync(userId);
        }
        catch
        {
            _failedUsers.Add(userId);
        }
    }

    private void OnOnlineUsersUpdated() => InvokeAsync(StateHasChanged);

    private void Close() => Dialog.Close();

    public void Dispose()
    {
        SyncPlay.OnlineUsersUpdated -= OnOnlineUsersUpdated;
    }
}

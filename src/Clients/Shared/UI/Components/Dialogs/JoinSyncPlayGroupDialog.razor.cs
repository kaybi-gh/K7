using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class JoinSyncPlayGroupDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    private string _groupCode = "";
    private bool _isJoining;
    private string? _error;

    private void Cancel() => Dialog.Cancel();

    private async Task JoinAsync()
    {
        if (string.IsNullOrWhiteSpace(_groupCode)) return;

        _isJoining = true;
        _error = null;
        try
        {
            if (!Guid.TryParse(_groupCode.Trim(), out var groupId))
            {
                _error = "Invalid group code.";
                return;
            }

            await SyncPlay.JoinGroupAsync(groupId);
            Dialog.Close(K7DialogResult.Ok());
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _isJoining = false;
        }
    }
}

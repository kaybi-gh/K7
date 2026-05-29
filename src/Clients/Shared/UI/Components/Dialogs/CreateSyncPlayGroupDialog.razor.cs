using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class CreateSyncPlayGroupDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid MediaReferenceId { get; set; }
    [Parameter] public string MediaTitle { get; set; } = "";
    [Parameter] public double MediaDuration { get; set; }
    [Parameter] public string? MediaCoverUrl { get; set; }

    private bool _isCreating;

    private void Cancel() => Dialog.Cancel();

    private async Task CreateAsync()
    {
        _isCreating = true;
        try
        {
            await SyncPlay.CreateGroupAsync(
                MediaReferenceId,
                MediaTitle,
                MediaDuration,
                MediaCoverUrl);

            Dialog.Close(K7DialogResult.Ok());
        }
        finally
        {
            _isCreating = false;
        }
    }
}

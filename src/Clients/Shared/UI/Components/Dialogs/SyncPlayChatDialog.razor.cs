using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class SyncPlayChatDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    private void Close() => Dialog.Close();
}

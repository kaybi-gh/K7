using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class SelectLocalUserDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public List<LocalUser> Users { get; set; } = [];

    private void Select(LocalUser user) => Dialog.Close(K7DialogResult.Ok(user));

    private void Cancel() => Dialog.Cancel();

    private static string GetInitial(LocalUser user)
    {
        var name = user.DisplayName ?? user.UserName;
        return string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class PasswordConfirmDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    private string _password = "";

    private void Submit() => Dialog.Close(K7DialogResult.Ok(_password));
    private void Cancel() => Dialog.Cancel();

    private void OnKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrEmpty(_password))
            Submit();
    }
}

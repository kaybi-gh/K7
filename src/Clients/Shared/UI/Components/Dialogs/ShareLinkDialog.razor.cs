using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Dialogs;

/// <summary>
/// Shows a freshly issued share link, once.
/// </summary>
public partial class ShareLinkDialog
{
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter, EditorRequired] public string Link { get; set; } = "";

    private void Close() => Dialog.Close(K7DialogResult.Ok(true));

    private async Task CopyLink()
    {
        await JS.InvokeVoidAsync("K7.shareOrCopy", Link);
        Snackbar.Add(L["LinkCopied"], K7Severity.Success);
    }
}

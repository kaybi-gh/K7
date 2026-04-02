using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class IndexedFilesDialog
{
    [CascadingParameter]
    IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public required MediaDto Media { get; set; }

    [Parameter]
    public EventCallback<Guid> OnReIdentifyFile { get; set; }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private async Task ReIdentifyFile(Guid fileId)
    {
        if (OnReIdentifyFile.HasDelegate)
        {
            await OnReIdentifyFile.InvokeAsync(fileId);
        }
        MudDialog.Close(DialogResult.Ok(true));
    }
}

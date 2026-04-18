using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class IndexedFilesDialog
{
    [CascadingParameter] IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public required MediaDto Media { get; set; }

    [Parameter]
    public EventCallback<Guid> OnReIdentifyFile { get; set; }

    private void Cancel()
    {
        Dialog.Cancel();
    }

    private async Task ReIdentifyFile(Guid fileId)
    {
        if (OnReIdentifyFile.HasDelegate)
        {
            await OnReIdentifyFile.InvokeAsync(fileId);
        }
        Dialog.Close(K7DialogResult.Ok(true));
    }
}

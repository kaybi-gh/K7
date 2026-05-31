using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class IndexedFilesDialog
{
    [CascadingParameter] IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public required MediaDto Media { get; set; }

    [Parameter]
    public EventCallback<Guid> OnReIdentifyFile { get; set; }

    [Inject] private IFederationService FederationService { get; set; } = default!;

    private Dictionary<Guid, IndexedFileDto?> _remoteFileDetails = [];
    private HashSet<Guid> _loadingFiles = [];

    protected override async Task OnInitializedAsync()
    {
        if (Media.RemoteIndexedFiles is { Count: > 0 })
        {
            foreach (var file in Media.RemoteIndexedFiles)
            {
                _ = LoadRemoteFileDetailsAsync(file.Id);
            }
        }
    }

    private async Task LoadRemoteFileDetailsAsync(Guid remoteFileId)
    {
        _loadingFiles.Add(remoteFileId);
        StateHasChanged();

        try
        {
            var details = await FederationService.GetRemoteFileDetailsAsync(remoteFileId);
            _remoteFileDetails[remoteFileId] = details;
        }
        catch
        {
            _remoteFileDetails[remoteFileId] = null;
        }
        finally
        {
            _loadingFiles.Remove(remoteFileId);
            StateHasChanged();
        }
    }

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

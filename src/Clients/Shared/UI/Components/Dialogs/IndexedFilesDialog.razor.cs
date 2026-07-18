using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Clients.Shared.Helpers;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class IndexedFilesDialog
{
    [CascadingParameter] IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public required MediaDto Media { get; set; }

    [Parameter]
    public EventCallback<Guid> OnReIdentifyFile { get; set; }

    [Inject] private IFederationService FederationService { get; set; } = default!;
    [Inject] private ILogger<IndexedFilesDialog> Logger { get; set; } = default!;

    private Dictionary<Guid, IndexedFileDto?> _remoteFileDetails = [];
    private HashSet<Guid> _loadingFiles = [];

    protected override async Task OnInitializedAsync()
    {
        if (Media.RemoteIndexedFiles is { Count: > 0 })
        {
            foreach (var file in Media.RemoteIndexedFiles)
            {
                LoadRemoteFileDetailsAsync(file.Id).FireAndForget(Logger);
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

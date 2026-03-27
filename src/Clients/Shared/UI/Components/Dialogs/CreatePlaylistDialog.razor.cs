using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class CreatePlaylistDialog
{
    [Inject] private IPlaylistService K7ServerService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    private string _title = "";
    private string? _description;
    private bool _isSubmitting;

    private void Cancel() => MudDialog.Cancel();

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_title)) return;

        _isSubmitting = true;
        try
        {
            var id = await K7ServerService.CreatePlaylistAsync(new CreatePlaylistRequest
            {
                Title = _title.Trim(),
                Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim()
            });

            Snackbar.Add("Playlist créée", Severity.Success);
            MudDialog.Close(DialogResult.Ok(id));
        }
        catch
        {
            Snackbar.Add("Erreur lors de la création", Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}

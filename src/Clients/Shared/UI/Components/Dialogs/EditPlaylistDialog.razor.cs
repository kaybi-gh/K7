using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class EditPlaylistDialog
{
    [Inject] private IPlaylistService K7ServerService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public Guid PlaylistId { get; set; }

    [Parameter]
    public string Title { get; set; } = "";

    [Parameter]
    public string? Description { get; set; }

    private bool _isSubmitting;

    private void Cancel() => MudDialog.Cancel();

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) return;

        _isSubmitting = true;
        try
        {
            await K7ServerService.UpdatePlaylistAsync(PlaylistId, new UpdatePlaylistRequest
            {
                Title = Title.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim()
            });

            Snackbar.Add(L["Updated"], Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch
        {
            Snackbar.Add(L["Error"], Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}

using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class EditPlaylistDialog
{
    [Inject] private IPlaylistService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public Guid PlaylistId { get; set; }

    [Parameter]
    public string Title { get; set; } = "";

    [Parameter]
    public string? Description { get; set; }

    [Parameter]
    public MediaType MediaType { get; set; } = MediaType.MusicTrack;

    private bool _isSubmitting;

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) return;

        _isSubmitting = true;
        try
        {
            await K7ServerService.UpdatePlaylistAsync(PlaylistId, new UpdatePlaylistRequest
            {
                Title = Title.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                MediaType = MediaType
            });

            Snackbar.Add(L["Updated"], K7Severity.Success);
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch
        {
            Snackbar.Add(L["Error"], K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}

using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;

namespace K7.Clients.Shared.UI.Helpers;

public static class MediaReviewDialogHelper
{
    public static async Task<bool> OpenAsync(
        IK7DialogService dialogService,
        IStringLocalizer<MediaReviewDialog> localizer,
        Guid mediaId,
        string? mediaTitle = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new K7DialogParameters<MediaReviewDialog>
        {
            { x => x.MediaId, mediaId },
            { x => x.MediaTitle, mediaTitle }
        };
        var options = new K7DialogOptions
        {
            MaxWidth = K7DialogMaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await dialogService.ShowAsync<MediaReviewDialog>(localizer["Title"], parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }
}

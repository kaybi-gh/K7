using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Models;

namespace K7.Clients.Shared.UI.Helpers;

public static class MediaReviewDetailDialogHelper
{
    public static async Task OpenAsync(
        IK7DialogService dialogService,
        IStringLocalizer<MediaReviewDetailDialog> localizer,
        MediaReviewCardModel review,
        CancellationToken cancellationToken = default)
    {
        var parameters = new K7DialogParameters<MediaReviewDetailDialog>
        {
            { x => x.Review, review }
        };
        var options = new K7DialogOptions
        {
            MaxWidth = K7DialogMaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await dialogService.ShowAsync<MediaReviewDetailDialog>(
            localizer["Title"],
            parameters,
            options);
        await dialog.Result;
    }
}

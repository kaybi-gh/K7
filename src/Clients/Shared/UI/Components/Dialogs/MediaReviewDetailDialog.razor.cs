using K7.Clients.Shared.UI.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class MediaReviewDetailDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Inject] private IStringLocalizer<MediaReviewDetailDialog> L { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter, EditorRequired] public MediaReviewCardModel Review { get; set; } = default!;

    private bool ShowProfileLink =>
        !string.IsNullOrEmpty(Review.ProfileHref) && !IsCurrentProfilePage(Review.ProfileHref);

    private void Close() => Dialog.Close();

    private void ViewProfile()
    {
        if (string.IsNullOrEmpty(Review.ProfileHref))
            return;

        var href = Review.ProfileHref;
        Dialog.Close();
        NavigationManager.NavigateTo(href);
    }

    private bool IsCurrentProfilePage(string profileHref)
    {
        var currentPath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri)
            .Split('?')[0]
            .TrimEnd('/');

        var targetPath = profileHref.Trim('/');

        return string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase);
    }
}

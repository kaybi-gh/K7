using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Constants;

namespace K7.Clients.Shared.UI.Components;

internal static class MediaCardExcludeActions
{
    public static async Task<(bool CanExclude, bool IsAdmin)> LoadPermissionsAsync(IFeatureAccessService featureAccess)
    {
        var role = await featureAccess.GetRoleAsync();
        return (
            role is not null and not Roles.Guest,
            role == Roles.Administrator);
    }

    public static async Task<bool> ExcludeForSelfAsync(
        MediaCardViewModel model,
        IUserAdminService userAdminService,
        IK7Snackbar snackbar,
        IStringLocalizer<SharedResource> strings)
    {
        try
        {
            var excluded = await userAdminService.ToggleMediaExclusionAsync(Guid.Parse(model.Id));
            snackbar.Add(
                excluded ? string.Format(strings["Hidden"], model.Title) : string.Format(strings["Unhidden"], model.Title),
                K7Severity.Success);
            return excluded;
        }
        catch (Exception ex)
        {
            snackbar.Add(string.Format(strings["ErrorWithDetails"], ex.Message), K7Severity.Error);
            return false;
        }
    }

    public static async Task ExcludeForOthersAsync(
        MediaCardViewModel model,
        IK7DialogService dialogService,
        IK7Snackbar snackbar,
        IStringLocalizer<SharedResource> strings)
    {
        var parameters = new K7DialogParameters<ExcludeMediaForUsersDialog>
        {
            { x => x.MediaId, Guid.Parse(model.Id) },
            { x => x.MediaTitle, model.Title }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await dialogService.ShowAsync<ExcludeMediaForUsersDialog>(strings["HideForUser"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            snackbar.Add(strings["ExclusionsUpdated"], K7Severity.Success);
    }
}

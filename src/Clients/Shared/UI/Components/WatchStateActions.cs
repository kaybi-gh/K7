using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Extensions;
using K7.Server.Domain.Constants;
using K7.Shared.Enums;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Components;

internal static class WatchStateActions
{
    public static bool SupportsWatchState(MediaCardKind kind) =>
        kind is MediaCardKind.Poster or MediaCardKind.Serie or MediaCardKind.Season or MediaCardKind.Episode;

    public static WatchStateScope GetScope(MediaCardKind kind) => kind switch
    {
        MediaCardKind.Serie => WatchStateScope.Serie,
        MediaCardKind.Season => WatchStateScope.Season,
        _ => WatchStateScope.Item
    };

    public static async Task<bool> CanSetWatchStateAsync(IFeatureAccessService featureAccess)
    {
        var role = await featureAccess.GetRoleAsync();
        return role is Roles.User or Roles.Administrator;
    }

    public static async Task<bool> ApplyAsync(
        IMediaService mediaService,
        MediaCacheStore cacheStore,
        IK7DialogService? dialogService,
        IK7Snackbar snackbar,
        IStringLocalizer<SharedResource> strings,
        Guid mediaId,
        bool watched,
        WatchStateScope scope,
        int? bulkEpisodeCount = null,
        CancellationToken cancellationToken = default)
    {
        if (bulkEpisodeCount is > 0 && dialogService is not null)
        {
            var title = watched ? strings["ConfirmMarkWatchedTitle"] : strings["ConfirmMarkUnwatchedTitle"];
            var message = watched
                ? string.Format(strings["ConfirmMarkWatchedMessage"], bulkEpisodeCount.Value)
                : string.Format(strings["ConfirmMarkUnwatchedMessage"], bulkEpisodeCount.Value);

            var confirmed = await dialogService.ShowMessageBoxAsync(
                title,
                message,
                strings["Confirm"].Value,
                strings["Cancel"].Value);

            if (confirmed != true)
                return false;
        }
        else if (scope is WatchStateScope.Serie or WatchStateScope.Season && dialogService is not null)
        {
            var title = watched ? strings["ConfirmMarkWatchedTitle"] : strings["ConfirmMarkUnwatchedTitle"];
            var message = watched ? strings["ConfirmMarkBulkWatchedMessage"] : strings["ConfirmMarkBulkUnwatchedMessage"];

            var confirmed = await dialogService.ShowMessageBoxAsync(
                title,
                message,
                strings["Confirm"].Value,
                strings["Cancel"].Value);

            if (confirmed != true)
                return false;
        }

        try
        {
            await mediaService.SetMediaWatchStateAsync(mediaId, watched, scope, cancellationToken);
            cacheStore.InvalidateHomeFeed();
            snackbar.Add(watched ? strings["MarkedAsWatched"] : strings["MarkedAsUnwatched"], K7Severity.Success);
            return true;
        }
        catch (Exception ex)
        {
            snackbar.Add(string.Format(strings["ErrorWithDetails"], ex.Message), K7Severity.Error);
            return false;
        }
    }

    public static void ApplyLocalCardState(MediaCardViewModel model, bool watched)
    {
        model.Watched = watched;
        model.Progress = watched ? 100 : 0;
    }
}

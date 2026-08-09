using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Merges a refreshed card into an existing instance so unchanged posters keep stable image URLs
/// (avoids K7Image remount / blink on soft visual updates).
/// </summary>
public static class MediaCardVisualMerge
{
    public readonly record struct Result(MediaCardViewModel Model, bool RequiresRender);

    public static Result Apply(MediaCardViewModel? existing, MediaCardViewModel next)
    {
        if (existing is null)
            return new(next, RequiresRender: true);

        var samePicture = MediaPictureUrlHelper.SameResourceUrl(existing.PictureUrl, next.PictureUrl);
        var sameBackdrop = MediaPictureUrlHelper.SameResourceUrl(existing.BackdropUrl, next.BackdropUrl);

        if (samePicture
            && sameBackdrop
            && existing.Title == next.Title
            && existing.AdditionalInformations == next.AdditionalInformations
            && existing.UserRating == next.UserRating)
        {
            var progressChanged = existing.Progress != next.Progress
                || existing.Watched != next.Watched
                || existing.GroupCount != next.GroupCount;
            existing.Watched = next.Watched;
            existing.Progress = next.Progress;
            existing.GroupCount = next.GroupCount;
            return new(existing, RequiresRender: progressChanged);
        }

        // Keep identical image URLs so K7Image does not remount for unchanged posters.
        if (samePicture || sameBackdrop)
        {
            next = next with
            {
                PictureUrl = samePicture ? existing.PictureUrl : next.PictureUrl,
                BackdropUrl = sameBackdrop ? existing.BackdropUrl : next.BackdropUrl
            };
        }

        return new(next, RequiresRender: true);
    }
}

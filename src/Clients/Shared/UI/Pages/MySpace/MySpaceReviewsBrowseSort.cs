using K7.Clients.Shared.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.MySpace;

internal static class MySpaceReviewsBrowseSort
{
    internal static readonly List<ReviewOrderingOption> Options =
    [
        ReviewOrderingOption.CreatedDesc,
        ReviewOrderingOption.CreatedAsc,
        ReviewOrderingOption.RatingDesc,
        ReviewOrderingOption.RatingAsc
    ];

    internal static string GetLabel(ReviewOrderingOption option, IStringLocalizer<MySpaceReviewsPage> localizer) =>
        option switch
        {
            ReviewOrderingOption.CreatedDesc => localizer["SortNewest"],
            ReviewOrderingOption.CreatedAsc => localizer["SortOldest"],
            ReviewOrderingOption.RatingDesc => localizer["SortRatingDesc"],
            ReviewOrderingOption.RatingAsc => localizer["SortRatingAsc"],
            _ => option.ToString()
        };

    internal static IEnumerable<SocialUserReviewViewDto> Apply(
        IEnumerable<SocialUserReviewViewDto> reviews,
        ReviewOrderingOption ordering) =>
        ordering switch
        {
            ReviewOrderingOption.CreatedAsc => reviews.OrderBy(r => r.Created),
            ReviewOrderingOption.RatingDesc => reviews.OrderByDescending(r => r.Rating).ThenByDescending(r => r.Created),
            ReviewOrderingOption.RatingAsc => reviews.OrderBy(r => r.Rating).ThenByDescending(r => r.Created),
            _ => reviews.OrderByDescending(r => r.Created)
        };
}

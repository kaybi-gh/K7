using K7.Shared.Home;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Helpers;

public static class HomeLayoutRowTitleHelper
{
    public static string Localize(IStringLocalizer localizer, string rowTitle)
    {
        if (HomeLayoutRowTitles.TryParseNewlyAddedIn(rowTitle, out var scope))
            return localizer["NewlyAddedIn", scope];

        if (HomeLayoutRowTitles.TryParseBecauseYouWatched(rowTitle, out var seedTitle))
            return localizer["BecauseYouWatched", seedTitle];

        return localizer[rowTitle];
    }
}

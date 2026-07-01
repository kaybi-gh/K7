using K7.Shared.Home;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Helpers;

public static class HomeLayoutRowTitleHelper
{
    public static string Localize(IStringLocalizer localizer, string rowTitle)
    {
        if (HomeLayoutRowTitles.TryParseNewlyAddedIn(rowTitle, out var scope))
            return localizer["NewlyAddedIn", scope];

        return localizer[rowTitle];
    }
}

using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.Helpers;

public static class ExploreNavigationHelper
{
    public static ExploreTapAction ResolveTapAction(
        Guid libraryGroupId,
        ExploreTapAction groupDefault,
        GeneralPreferencesDto? preferences) =>
        preferences?.ResolveExploreTapAction(libraryGroupId, groupDefault) ?? groupDefault;

    public static string GetCategoryHref(Guid libraryGroupId, ExploreTapAction action) =>
        action == ExploreTapAction.Browse
            ? $"/library-groups/{libraryGroupId}"
            : $"/explore?library-group={libraryGroupId}";
}

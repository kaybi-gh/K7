using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;

namespace K7.Shared.CustomNav;

public static class CustomNavSanitizer
{
    public static CustomNavLayoutDto Sanitize(CustomNavLayoutDto layout, IReadOnlySet<Guid> validLibraryGroupIds) =>
        Sanitize(layout, validLibraryGroupIds, null, null);

    public static CustomNavLayoutDto Sanitize(
        CustomNavLayoutDto layout,
        IReadOnlySet<Guid> validLibraryGroupIds,
        IReadOnlySet<Guid>? validCollectionIds,
        IReadOnlySet<Guid>? validPlaylistIds,
        bool allowAdminRoutes = false)
    {
        var items = new List<CustomNavItemDto>();
        var seen = new HashSet<Guid>();
        var collections = validCollectionIds ?? new HashSet<Guid>();
        var playlists = validPlaylistIds ?? new HashSet<Guid>();

        foreach (var item in layout.Items)
        {
            if (items.Count >= CustomNavLimits.MaxItems)
                break;

            var cleaned = SanitizeItem(item, validLibraryGroupIds, collections, playlists, allowAdminRoutes);
            if (cleaned is null)
                continue;

            var id = cleaned.Id;
            if (id == Guid.Empty || !seen.Add(id))
                cleaned = cleaned with { Id = Guid.NewGuid() };

            items.Add(cleaned);
        }

        var placement = Enum.IsDefined(layout.Placement) ? layout.Placement : CustomNavPlacement.FeedRow;
        var showBarOnHome = layout.ShowBarOnHome;
        var showBarOnExplore = layout.ShowBarOnExplore;
        var showBarOnMySpace = layout.ShowBarOnMySpace;
        var showBarOnSettings = layout.ShowBarOnSettings;
        var showBarOnMedia = layout.ShowBarOnMedia;
        if (layout.BarScope == CustomNavBarScope.Everywhere)
        {
            showBarOnHome = true;
            showBarOnExplore = true;
            showBarOnMySpace = true;
            showBarOnSettings = true;
            showBarOnMedia = true;
        }

        if (!showBarOnHome && !showBarOnExplore && !showBarOnMySpace && !showBarOnSettings && !showBarOnMedia)
        {
            showBarOnHome = true;
            showBarOnExplore = true;
        }

        var showRowOnHome = layout.ShowRowOnHome;
        var showRowOnExploreFeeds = layout.ShowRowOnExploreFeeds;
        if (layout.FeedRowScope == CustomNavFeedRowScope.HomeAndGroupFeeds)
            showRowOnExploreFeeds = true;
        if (!showRowOnHome && !showRowOnExploreFeeds)
            showRowOnHome = true;

        var showOnDesktop = layout.ShowOnDesktop;
        var showOnPhone = placement == CustomNavPlacement.Bar ? false : layout.ShowOnPhone;
        var showOnTv = layout.ShowOnTv;
        if (!showOnDesktop && !showOnPhone && !showOnTv)
        {
            showOnDesktop = true;
            showOnTv = true;
        }

        return new CustomNavLayoutDto
        {
            Enabled = layout.Enabled && items.Count > 0,
            Placement = placement,
            BarShowIcons = layout.BarShowIcons,
            FeedRowTitle = NormalizeTitle(layout.FeedRowTitle),
            ShowRowOnHome = showRowOnHome,
            ShowRowOnExploreFeeds = showRowOnExploreFeeds,
            ShowBarOnHome = showBarOnHome,
            ShowBarOnExplore = showBarOnExplore,
            ShowBarOnMySpace = showBarOnMySpace,
            ShowBarOnSettings = showBarOnSettings,
            ShowBarOnMedia = showBarOnMedia,
            ShowOnDesktop = showOnDesktop,
            ShowOnPhone = showOnPhone,
            ShowOnTv = showOnTv,
            Items = items
        };
    }

    public static bool HasChanges(CustomNavLayoutDto original, CustomNavLayoutDto sanitized) =>
        original.Enabled != sanitized.Enabled
        || original.Placement != sanitized.Placement
        || original.BarShowIcons != sanitized.BarShowIcons
        || original.FeedRowTitle != sanitized.FeedRowTitle
        || original.ShowRowOnHome != sanitized.ShowRowOnHome
        || original.ShowRowOnExploreFeeds != sanitized.ShowRowOnExploreFeeds
        || original.ShowBarOnHome != sanitized.ShowBarOnHome
        || original.ShowBarOnExplore != sanitized.ShowBarOnExplore
        || original.ShowBarOnMySpace != sanitized.ShowBarOnMySpace
        || original.ShowBarOnSettings != sanitized.ShowBarOnSettings
        || original.ShowBarOnMedia != sanitized.ShowBarOnMedia
        || original.ShowOnDesktop != sanitized.ShowOnDesktop
        || original.ShowOnPhone != sanitized.ShowOnPhone
        || original.ShowOnTv != sanitized.ShowOnTv
        || original.Items.Count != sanitized.Items.Count
        || original.Items.Zip(sanitized.Items).Any(pair => pair.First != pair.Second);

    private static CustomNavItemDto? SanitizeItem(
        CustomNavItemDto item,
        IReadOnlySet<Guid> validLibraryGroupIds,
        IReadOnlySet<Guid> validCollectionIds,
        IReadOnlySet<Guid> validPlaylistIds,
        bool allowAdminRoutes)
    {
        if (!Enum.IsDefined(item.Kind))
            return null;

        var cleaned = item with
        {
            Title = NormalizeTitle(item.Title),
            Icon = NormalizeIcon(item.Icon),
            CardColor = NormalizeCardColor(item.CardColor),
            CoverPictureId = item.CoverPictureId is { } pictureId && pictureId != Guid.Empty
                ? pictureId
                : null
        };

        return cleaned.Kind switch
        {
            CustomNavItemKind.LibraryGroup => SanitizeLibraryGroup(cleaned, validLibraryGroupIds),
            CustomNavItemKind.LibraryBrowse => SanitizeLibraryBrowse(cleaned, validLibraryGroupIds),
            CustomNavItemKind.AppRoute => SanitizeAppRoute(cleaned, allowAdminRoutes, expectAdmin: false),
            CustomNavItemKind.AdminRoute => SanitizeAppRoute(cleaned, allowAdminRoutes, expectAdmin: true),
            CustomNavItemKind.Collection => SanitizePinned(cleaned, validCollectionIds),
            CustomNavItemKind.Playlist or CustomNavItemKind.DynamicPlaylist => SanitizePinned(cleaned, validPlaylistIds),
            _ => null
        };
    }

    private static CustomNavItemDto? SanitizeLibraryGroup(
        CustomNavItemDto item,
        IReadOnlySet<Guid> validLibraryGroupIds)
    {
        if (item.LibraryGroupId is not { } groupId || !validLibraryGroupIds.Contains(groupId))
            return null;

        return item with
        {
            LibraryGroupId = groupId,
            BrowseQuery = null,
            Route = null,
            TargetId = null
        };
    }

    private static CustomNavItemDto? SanitizeLibraryBrowse(
        CustomNavItemDto item,
        IReadOnlySet<Guid> validLibraryGroupIds)
    {
        if (item.LibraryGroupId is not { } groupId || !validLibraryGroupIds.Contains(groupId))
            return null;

        return item with
        {
            LibraryGroupId = groupId,
            TapAction = null,
            BrowseQuery = CustomNavRoutes.NormalizeBrowseQuery(item.BrowseQuery),
            Route = null,
            TargetId = null
        };
    }

    private static CustomNavItemDto? SanitizeAppRoute(
        CustomNavItemDto item,
        bool allowAdminRoutes,
        bool expectAdmin)
    {
        if (!CustomNavRoutes.TryNormalize(item.Route, out var route))
            return null;

        var app = CustomNavRoutes.Find(route);
        var isAdmin = app is { AdminOnly: true };
        if (isAdmin && !allowAdminRoutes)
            return null;

        if (expectAdmin && !isAdmin)
            return null;

        return item with
        {
            Kind = isAdmin ? CustomNavItemKind.AdminRoute : CustomNavItemKind.AppRoute,
            Icon = item.Icon ?? app?.Icon,
            LibraryGroupId = null,
            TapAction = null,
            BrowseQuery = null,
            Route = route,
            TargetId = null
        };
    }

    private static CustomNavItemDto? SanitizePinned(CustomNavItemDto item, IReadOnlySet<Guid> validIds)
    {
        if (item.TargetId is not { } targetId || !validIds.Contains(targetId))
            return null;

        return item with
        {
            TargetId = targetId,
            LibraryGroupId = null,
            TapAction = null,
            BrowseQuery = null,
            Route = null
        };
    }

    private static string? NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var trimmed = title.Trim();
        return trimmed.Length > CustomNavLimits.MaxTitleLength
            ? trimmed[..CustomNavLimits.MaxTitleLength]
            : trimmed;
    }

    private static string? NormalizeIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
            return null;

        var trimmed = icon.Trim();
        return trimmed.Length > 64 ? null : trimmed;
    }

    private static string? NormalizeCardColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;

        var trimmed = color.Trim();
        if (trimmed.Length == 6)
            trimmed = "#" + trimmed;

        if (trimmed.Length != CustomNavLimits.MaxCardColorLength || trimmed[0] != '#')
            return null;

        for (var i = 1; i < trimmed.Length; i++)
        {
            if (!char.IsAsciiHexDigit(trimmed[i]))
                return null;
        }

        return trimmed;
    }
}

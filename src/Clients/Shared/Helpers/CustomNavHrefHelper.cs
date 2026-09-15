using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.CustomNav;
using K7.Shared.Dtos;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Enums;

namespace K7.Clients.Shared.Helpers;

public static class CustomNavHrefHelper
{
    public static string GetHref(
        CustomNavItemDto item,
        IReadOnlyList<LibraryGroupDto> groups,
        GeneralPreferencesDto? preferences,
        IReadOnlyList<LiteCollectionDto>? collections = null,
        IReadOnlyList<LitePlaylistDto>? playlists = null)
    {
        return item.Kind switch
        {
            CustomNavItemKind.AppRoute or CustomNavItemKind.AdminRoute => item.Route ?? "/",
            CustomNavItemKind.LibraryBrowse => BuildBrowseHref(item),
            CustomNavItemKind.LibraryGroup => BuildGroupHref(item, groups, preferences),
            CustomNavItemKind.Collection => item.TargetId is { } collectionId
                ? $"/collections/{collectionId}"
                : "/",
            CustomNavItemKind.DynamicPlaylist => item.TargetId is { } dynamicId
                ? $"/dynamic-playlists/{dynamicId}"
                : "/",
            CustomNavItemKind.Playlist => item.TargetId is { } playlistId
                ? ResolvePlaylistHref(playlistId, playlists)
                : "/",
            _ => "/"
        };
    }

    public static string GetTitle(
        CustomNavItemDto item,
        IReadOnlyList<LibraryGroupDto> groups,
        Func<string, string>? localizeRoute,
        IReadOnlyList<LiteCollectionDto>? collections = null,
        IReadOnlyList<LitePlaylistDto>? playlists = null)
    {
        if (!string.IsNullOrWhiteSpace(item.Title))
            return item.Title;

        if (item.Kind is CustomNavItemKind.AppRoute or CustomNavItemKind.AdminRoute)
        {
            var route = CustomNavRoutes.Find(item.Route);
            if (route is not null)
                return localizeRoute?.Invoke(route.LabelKey) ?? route.Path;
        }

        if (item.Kind == CustomNavItemKind.Collection)
            return FindCollection(item, collections)?.Title ?? "";

        if (item.Kind is CustomNavItemKind.Playlist or CustomNavItemKind.DynamicPlaylist)
            return FindPlaylist(item, playlists)?.Title ?? "";

        var group = FindGroup(item, groups);
        return group?.Title ?? item.Route ?? "";
    }

    public static string ToMenuIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
            return "";

        var value = icon.Trim();
        if (value.StartsWith("ph ", StringComparison.Ordinal))
            value = value[3..].Trim();
        if (value.StartsWith("ph-", StringComparison.Ordinal))
            value = value[3..];

        return value;
    }

    public static string? GetIcon(
        CustomNavItemDto item,
        IReadOnlyList<LibraryGroupDto> groups,
        IReadOnlyList<LiteCollectionDto>? collections = null,
        IReadOnlyList<LitePlaylistDto>? playlists = null)
    {
        if (!string.IsNullOrWhiteSpace(item.Icon))
            return item.Icon;

        if (item.Kind is CustomNavItemKind.AppRoute or CustomNavItemKind.AdminRoute)
            return CustomNavRoutes.Find(item.Route)?.Icon;

        if (item.Kind == CustomNavItemKind.Collection)
            return "bookmark-simple";

        if (item.Kind == CustomNavItemKind.DynamicPlaylist)
            return "sparkle";

        if (item.Kind == CustomNavItemKind.Playlist)
            return FindPlaylist(item, playlists)?.IsDynamicPlaylist == true ? "sparkle" : "queue";

        var group = FindGroup(item, groups);
        if (!string.IsNullOrEmpty(group?.Icon))
            return group.Icon;

        return group?.MediaType switch
        {
            LibraryMediaType.Movie => "film-strip",
            LibraryMediaType.Serie => "television",
            LibraryMediaType.Music => "music-notes",
            _ => "folder"
        };
    }

    public static string? GetCardColor(
        CustomNavItemDto item,
        IReadOnlyList<LibraryGroupDto> groups)
    {
        if (!string.IsNullOrWhiteSpace(item.CardColor))
            return item.CardColor;

        if (item.Kind is CustomNavItemKind.AppRoute or CustomNavItemKind.AdminRoute)
            return CustomNavRoutes.Find(item.Route)?.CardColor;

        return FindGroup(item, groups)?.CardColor;
    }

    public static Guid? GetCoverPictureId(
        CustomNavItemDto item,
        IReadOnlyList<LibraryGroupDto> groups,
        IReadOnlyList<LiteCollectionDto>? collections = null,
        IReadOnlyList<LitePlaylistDto>? playlists = null)
    {
        if (item.CoverPictureId is { } overrideId)
            return overrideId;

        if (item.Kind == CustomNavItemKind.Collection)
            return FindCollection(item, collections)?.CoverPicture?.Id;

        if (item.Kind is CustomNavItemKind.Playlist or CustomNavItemKind.DynamicPlaylist)
            return FindPlaylist(item, playlists)?.CoverPicture?.Id;

        return FindGroup(item, groups)?.CoverPictureId;
    }

    public static (string GradientStart, string IconColor) GetCardTone(
        CustomNavItemDto item,
        IReadOnlyList<LibraryGroupDto> groups)
    {
        var group = FindGroup(item, groups);
        var hex = GetCardColor(item, groups)
            ?? (group is not null
                ? LibraryGroupCardColors.GetDefaultHex(group.MediaType)
                : "#283040");
        var rgba = LibraryGroupCardColors.ToRgba(hex);
        return (rgba.GradientStart, rgba.IconColor);
    }

    public static string GetInheritedCardColor(CustomNavItemDto item, IReadOnlyList<LibraryGroupDto> groups)
    {
        if (item.Kind is CustomNavItemKind.AppRoute or CustomNavItemKind.AdminRoute)
            return CustomNavRoutes.Find(item.Route)?.CardColor ?? "#283040";

        var group = FindGroup(item, groups);
        return group is not null
            ? group.CardColor ?? LibraryGroupCardColors.GetDefaultHex(group.MediaType)
            : "#283040";
    }

    public static LibraryGroupDto? FindGroup(CustomNavItemDto item, IReadOnlyList<LibraryGroupDto> groups) =>
        item.LibraryGroupId is { } id
            ? groups.FirstOrDefault(g => g.Id == id)
            : null;

    public static LiteCollectionDto? FindCollection(
        CustomNavItemDto item,
        IReadOnlyList<LiteCollectionDto>? collections) =>
        item.TargetId is { } id
            ? collections?.FirstOrDefault(c => c.Id == id)
            : null;

    public static LitePlaylistDto? FindPlaylist(
        CustomNavItemDto item,
        IReadOnlyList<LitePlaylistDto>? playlists) =>
        item.TargetId is { } id
            ? playlists?.FirstOrDefault(p => p.Id == id)
            : null;

    private static string ResolvePlaylistHref(Guid playlistId, IReadOnlyList<LitePlaylistDto>? playlists)
    {
        var playlist = playlists?.FirstOrDefault(p => p.Id == playlistId);
        return playlist?.IsDynamicPlaylist == true
            ? $"/dynamic-playlists/{playlistId}"
            : $"/playlists/{playlistId}";
    }

    private static string BuildBrowseHref(CustomNavItemDto item)
    {
        if (item.LibraryGroupId is not { } groupId)
            return "/";

        return string.IsNullOrEmpty(item.BrowseQuery)
            ? $"/library-groups/{groupId}"
            : $"/library-groups/{groupId}?{item.BrowseQuery}";
    }

    private static string BuildGroupHref(
        CustomNavItemDto item,
        IReadOnlyList<LibraryGroupDto> groups,
        GeneralPreferencesDto? preferences)
    {
        if (item.LibraryGroupId is not { } groupId)
            return "/";

        var group = FindGroup(item, groups);
        var fallback = group?.ExploreTapAction ?? ExploreTapAction.Suggestions;
        var action = item.TapAction
            ?? ExploreNavigationHelper.ResolveTapAction(groupId, fallback, preferences);
        return ExploreNavigationHelper.GetCategoryHref(groupId, action);
    }
}

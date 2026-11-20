using System.Text.Json;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;

namespace K7.Shared.Theming;

/// <summary>
/// The skins of media pages and players (the <c>MediaPage*</c>, <c>SeasonPage</c>,
/// <c>Audio*</c>, <c>MiniPlayer</c> and <c>Video*</c> members of
/// <see cref="AppearanceSettingsDto"/>) as a set of their own: what an administrator stores as
/// the defaults of the server.
/// </summary>
public static class AppearanceSkins
{
    public static bool IsValid(AppearanceSettingsDto? settings) =>
        settings is not null
        && Enum.IsDefined(settings.MediaPageHero)
        && Enum.IsDefined(settings.MediaPageHeroSize)
        && Enum.IsDefined(settings.MediaPageLists)
        && Enum.IsDefined(settings.SeasonPage)
        && Enum.IsDefined(settings.AudioBackground)
        && Enum.IsDefined(settings.AudioLayout)
        && Enum.IsDefined(settings.AudioArtwork)
        && Enum.IsDefined(settings.AudioSeek)
        && Enum.IsDefined(settings.AudioAccent)
        && Enum.IsDefined(settings.MiniPlayer)
        && Enum.IsDefined(settings.VideoControls)
        && Enum.IsDefined(settings.VideoAccent)
        && settings.MediaPageOverrides is not null
        && settings.MediaPageOverrides.All(p => Enum.IsDefined(p.Key) && IsValid(p.Value));

    public static bool IsValid(MediaPageSkinDto? skin) =>
        skin is not null
        && Enum.IsDefined(skin.Hero)
        && Enum.IsDefined(skin.HeroSize)
        && Enum.IsDefined(skin.Lists);

    /// <summary>The skins alone: nothing of the look (theme, accent, text) comes along.</summary>
    public static AppearanceSettingsDto Only(AppearanceSettingsDto settings)
    {
        var skins = new AppearanceSettingsDto();
        skins.CopyMediaPagesFrom(settings);
        skins.CopyAudioPlayerFrom(settings);
        skins.CopyVideoPlayerFrom(settings);
        return skins;
    }

    public static string Serialize(AppearanceSettingsDto settings) => JsonSerializer.Serialize(Only(settings));

    /// <summary>The stored skins, or null when there are none, they are the built-in ones, or they cannot be read.</summary>
    public static AppearanceSettingsDto? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var settings = JsonSerializer.Deserialize<AppearanceSettingsDto>(json);
            if (!IsValid(settings))
                return null;

            var skins = Only(settings!);
            return skins.IsDefault ? null : skins;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsValid(ServerSkinsDto? skins) =>
        skins is not null
        && IsValidServerSet(skins.Default)
        && skins.DeviceOverrides is not null
        && skins.DeviceOverrides.All(p => Enum.IsDefined(p.Key) && p.Key != DeviceType.Unknown && IsValidServerSet(p.Value));

    // A set of the server also carries corners and, for a type of device, a theme: built-in or
    // of the server, never the theme of an account.
    private static bool IsValidServerSet(AppearanceSettingsDto? settings) =>
        IsValid(settings)
        && Enum.IsDefined(settings!.CornerStyle)
        && (settings.ThemeId is null || BuiltInThemes.Contains(settings.ThemeId) || ServerThemes.IsValidId(settings.ThemeId));

    /// <summary>
    /// The sets as they are stored: skins and corners, and no set for a type of device that
    /// shows the same thing as the shared one. The theme shared by every device is the default
    /// theme of the server, stored on its own: only a type of device keeps one here, and only
    /// to differ from it (null follows it).
    /// </summary>
    public static ServerSkinsDto Normalize(ServerSkinsDto skins)
    {
        var shared = Only(skins.Default);
        shared.CornerStyle = skins.Default.CornerStyle;
        var sharedJson = JsonSerializer.Serialize(shared);
        var normalized = new ServerSkinsDto { Default = shared };
        foreach (var (deviceType, settings) in skins.DeviceOverrides)
        {
            var own = Only(settings);
            own.CornerStyle = settings.CornerStyle;
            own.ThemeId = settings.ThemeId == skins.Default.ThemeId ? null : settings.ThemeId;
            if (JsonSerializer.Serialize(own) != sharedJson)
                normalized.DeviceOverrides[deviceType] = own;
        }

        return normalized;
    }

    public static string Serialize(ServerSkinsDto skins) => JsonSerializer.Serialize(Normalize(skins));

    /// <summary>
    /// The stored sets, or null when there are none, they are all the built-in ones, or they
    /// cannot be read. A server set up before device types had their own set stored the shared
    /// set alone: it is read as such.
    /// </summary>
    public static ServerSkinsDto? ParseSet(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            ServerSkinsDto? skins;
            if (document.RootElement.TryGetProperty(nameof(ServerSkinsDto.Default), out _)
                || document.RootElement.TryGetProperty(nameof(ServerSkinsDto.DeviceOverrides), out _))
            {
                skins = document.RootElement.Deserialize<ServerSkinsDto>();
            }
            else
            {
                var shared = document.RootElement.Deserialize<AppearanceSettingsDto>();
                skins = shared is null ? null : new ServerSkinsDto { Default = shared };
            }

            if (!IsValid(skins))
                return null;

            var normalized = Normalize(skins!);
            return normalized.Default.IsDefault && normalized.DeviceOverrides.Count == 0 ? null : normalized;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

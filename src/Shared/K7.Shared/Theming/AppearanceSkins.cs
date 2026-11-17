using System.Text.Json;
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
}

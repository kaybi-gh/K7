using K7.Shared.Theming;

namespace K7.Server.Application.Features.Appearance.Commands.UpdateAppearancePreferences;

public class UpdateAppearancePreferencesCommandValidator : AbstractValidator<UpdateAppearancePreferencesCommand>
{
    public UpdateAppearancePreferencesCommandValidator()
    {
        RuleFor(x => x.Preferences)
            .NotNull()
            .Must(BeValid)
            .WithMessage("Appearance preferences are not valid.");
    }

    private static bool BeValid(AppearancePreferencesDto? preferences)
    {
        if (preferences?.Default is null || preferences.DeviceOverrides is null || preferences.CustomThemes is null)
            return false;

        if (!IsValidSettings(preferences.Default))
            return false;

        if (preferences.DeviceOverrides.Any(o => !Enum.IsDefined(o.Key) || !IsValidSettings(o.Value)))
            return false;

        if (preferences.Cards is not null && !MediaCardStyles.IsValid(preferences.Cards))
            return false;

        if (preferences.CustomThemes.Count > AppearancePreferencesDto.MaxCustomThemes
            || preferences.CustomThemes.Any(t => !ThemePaletteValidator.IsValidTheme(t, CustomThemeDto.IdPrefix)))
        {
            return false;
        }

        return preferences.CustomThemes.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count()
               == preferences.CustomThemes.Count;
    }

    private static bool IsValidSettings(AppearanceSettingsDto? settings) =>
        settings is not null
        && (settings.ThemeId is null
            || BuiltInThemes.Contains(settings.ThemeId)
            || ThemePaletteValidator.IsValidCustomThemeId(settings.ThemeId)
            || ServerThemes.IsValidId(settings.ThemeId))
        && (settings.AccentColor is null || ThemeColorMath.IsHex(settings.AccentColor))
        && Enum.IsDefined(settings.TextSize)
        && Enum.IsDefined(settings.CornerStyle)
        && Enum.IsDefined(settings.MiniPlayer)
        && AppearanceSkins.IsValid(settings);

    private static bool IsValidCustomTheme(CustomThemeDto? theme) =>
        theme is not null
        && ThemePaletteValidator.IsValidCustomThemeId(theme.Id)
        && !string.IsNullOrWhiteSpace(theme.Name)
        && theme.Name.Length <= CustomThemeDto.MaxNameLength
        && ThemePaletteValidator.HasValidColors(theme.Palette);
}

using K7.Clients.Shared.Services;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

/// <summary>
/// Creates or edits a custom theme. The whole app previews the colors while the dialog is
/// open; saving is only possible once the palette passes the readability checks.
/// </summary>
public partial class ThemeEditorDialog : IDisposable
{
    private sealed record ColorField(
        string LabelKey,
        string? HintKey,
        Func<ThemePaletteDto, string> Get,
        Action<ThemePaletteDto, string> Set);

    private static readonly IReadOnlyList<ColorField> Fields =
    [
        new("Color_Background", null, p => p.Background, (p, v) => p.Background = v),
        new("Color_Surface", "Color_Surface_Hint", p => p.Surface, (p, v) => p.Surface = v),
        new("Color_SurfaceAlt", "Color_SurfaceAlt_Hint", p => p.SurfaceAlt, (p, v) => p.SurfaceAlt = v),
        new("Color_Text", null, p => p.Text, (p, v) => p.Text = v),
        new("Color_TextSecondary", null, p => p.TextSecondary, (p, v) => p.TextSecondary = v),
        new("Color_TextMuted", "Color_TextMuted_Hint", p => p.TextMuted, (p, v) => p.TextMuted = v),
        new("Color_Accent", "Color_Accent_Hint", p => p.Accent, (p, v) => p.Accent = v),
        new("Color_OnAccent", "Color_OnAccent_Hint", p => p.OnAccent, (p, v) => p.OnAccent = v),
        new("Color_AccentText", "Color_AccentText_Hint", p => p.AccentText, (p, v) => p.AccentText = v)
    ];

    [Inject] private ThemeService ThemeService { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    /// <summary>Theme to edit. Null starts a new theme from the colors currently shown.</summary>
    [Parameter] public CustomThemeDto? Theme { get; set; }

    private string _id = "";
    private string? _name = "";
    private ThemePaletteDto _palette = new();
    private IReadOnlyList<ThemePaletteIssue> _issues = [];

    private IReadOnlyList<ButtonGroupOption<bool>> BaseOptions =>
    [
        new(true, L["BaseDark"]),
        new(false, L["BaseLight"])
    ];

    private bool CanSave => !string.IsNullOrWhiteSpace(_name) && _issues.Count == 0;

    protected override void OnInitialized()
    {
        if (Theme is not null)
        {
            _id = Theme.Id;
            _name = Theme.Name;
            _palette = Theme.Palette with { };
        }
        else
        {
            _id = CustomThemeDto.NewId();
            _palette = ThemeService.Theme.Palette with { };
        }

        Refresh();
    }

    public void Dispose() => ThemeService.SetPreview(null);

    private void OnBaseChanged(bool isDark)
    {
        _palette.IsDark = isDark;
        Refresh();
    }

    private void OnColorChanged(ColorField field, string value)
    {
        if (!ThemeColorMath.IsHex(value))
            return;

        field.Set(_palette, ThemeColorMath.Normalize(value));
        Refresh();
    }

    private void Refresh()
    {
        _issues = ThemePaletteValidator.Validate(_palette);
        ThemeService.SetPreview(new ThemeDefinition(_id, _palette with { }, IsBuiltIn: false, CustomName: _name));
    }

    private void Cancel() => Dialog.Cancel();

    private void Save()
    {
        if (!CanSave)
            return;

        Dialog.Close(K7DialogResult.Ok(new CustomThemeDto
        {
            Id = _id,
            Name = _name!.Trim(),
            Palette = _palette with { }
        }));
    }
}

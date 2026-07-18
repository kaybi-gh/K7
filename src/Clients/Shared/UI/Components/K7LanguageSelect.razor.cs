using System.Globalization;
using K7.Shared;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7LanguageSelect
{
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string Variant { get; set; } = "outlined";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// Which language set to display. Defaults to Metadata (the larger set).
    /// Use SupportedLanguages.Interface for UI-only language selection.
    /// </summary>
    [Parameter] public IReadOnlyList<LanguageOption> Languages { get; set; } = SupportedLanguages.Metadata;

    private static string GetDisplayText(LanguageOption lang)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(lang.Code);
            var translated = culture.DisplayName;
            if (string.IsNullOrEmpty(translated) || string.Equals(translated, lang.NativeLabel, StringComparison.OrdinalIgnoreCase))
            {
                return lang.NativeLabel;
            }

            return $"{lang.NativeLabel} ({translated})";
        }
        catch
        {
            return lang.NativeLabel;
        }
    }
}

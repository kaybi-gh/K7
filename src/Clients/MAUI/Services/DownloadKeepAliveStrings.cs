using System.Globalization;

namespace K7.Clients.MAUI.Services;

/// <summary>
/// FR/EN fallback strings for the Android download notification. Blazor uses IStringLocalizer + .resx;
/// native notifications are outside Razor so we mirror the two locales here.
/// </summary>
internal static class DownloadKeepAliveStrings
{
    private static bool IsFrench =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fr", StringComparison.OrdinalIgnoreCase);

    public static string ChannelName => IsFrench ? "Téléchargements" : "Downloads";
    public static string Title => IsFrench ? "Téléchargement" : "Downloading";
    public static string TitleWithCount(int count) =>
        IsFrench ? $"Téléchargement ({count})" : $"Downloading ({count})";
    public static string Preparing => IsFrench ? "Préparation..." : "Preparing...";
    public static string Queued => IsFrench ? "En attente" : "Queued";
    public static string Cancel => IsFrench ? "Annuler" : "Cancel";
}

using System.Globalization;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// FR/EN fallback strings for the native video overlay. Blazor uses IStringLocalizer + .resx;
/// native XAML is outside Razor so we mirror the two locales here. User-facing French uses
/// accents; code/comments stay ASCII punctuation.
/// </summary>
internal static class NativeStrings
{
    private static bool IsFrench =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fr", StringComparison.OrdinalIgnoreCase);

    public static string SkipIntro => IsFrench ? "Passer l'intro" : "Skip intro";
    public static string SkipOutro => IsFrench ? "Passer le générique" : "Skip outro";
    public static string IntroSkipped => IsFrench ? "Intro passée" : "Intro skipped";
    public static string OutroSkipped => IsFrench ? "Générique passé" : "Outro skipped";
    public static string Intro => "Intro";
    public static string Outro => IsFrench ? "Générique" : "Outro";

    public static string NextEpisode => IsFrench ? "Épisode suivant" : "Next episode";
    public static string CurrentEpisode => IsFrench ? "Épisode en cours" : "Current episode";
    public static string PlayNow => IsFrench ? "Regarder" : "Play now";
    public static string Replay => IsFrench ? "Revoir" : "Replay";
    public static string Dismiss => IsFrench ? "Fermer" : "Dismiss";
    public static string NoMoreEpisodes => IsFrench ? "Plus d'épisodes" : "No more episodes";
    public static string AutoPlayIn(int seconds) =>
        IsFrench ? $"Lecture automatique dans {seconds}s" : $"Playing next in {seconds}s";

    public static string WatchTogether => IsFrench ? "Regarder ensemble" : "Watch together";
    public static string LeaveGroup => IsFrench ? "Quitter le groupe" : "Leave group";
    public static string WaitingForOthers => IsFrench ? "En attente des autres..." : "Waiting for others...";
    public static string MessagePlaceholder => "Message...";

    public static string CastToDevice => IsFrench ? "Diffuser sur un appareil" : "Cast to device";
    public static string Chromecast => "Chromecast";
    public static string RemoteDevices => IsFrench ? "Appareils distants" : "Remote devices";
    public static string SearchingForDevices => IsFrench ? "Recherche d'appareils..." : "Searching for devices...";

    public static string Back => IsFrench ? "Retour" : "Back";
    public static string Close => IsFrench ? "Fermer" : "Close";
    public static string PlaybackSettings => IsFrench ? "Paramètres de lecture" : "Playback settings";
    public static string Audio => "Audio";
    public static string Subtitles => IsFrench ? "Sous-titres" : "Subtitles";
    public static string SubtitlesOff => IsFrench ? "Désactivés" : "Off";
    public static string Quality => IsFrench ? "Qualité" : "Quality";
    public static string Speed => IsFrench ? "Vitesse" : "Speed";
    public static string AspectRatio => IsFrench ? "Format d'image" : "Aspect ratio";
    public static string Normal => "Normal";
    public static string Fit => IsFrench ? "Ajusté" : "Fit";
    public static string Fill => IsFrench ? "Rempli" : "Fill";
    public static string Stretch => IsFrench ? "Étiré" : "Stretch";
}

namespace K7.Shared.Constants;

public static class UserPreferenceKeys
{
    public const string GeneralPreferences = "GeneralPreferences";
    public const string HomeLayout = "HomeLayout";
    public const string CustomNavLayout = "CustomNavLayout";
    public const string AudioPlayerSettings = "AudioPlayerSettings";
    public const string AudioPlaybackPolicy = "AudioPlaybackPolicy";
    public const string VideoPlayerSettings = "VideoPlayerSettings";
    public const string VideoPlaybackPolicy = "VideoPlaybackPolicy";
    public const string TrackSelectionPreferences = "TrackSelectionPreferences";

    public static string TrackSelectionForLibrary(Guid libraryId) =>
        $"TrackSelectionPreferences:Library:{libraryId}";

    public static string LastVideoTrackSelectionForMovie(Guid mediaId) =>
        $"LastVideoTrackSelection:Movie:{mediaId}";

    public static string LastVideoTrackSelectionForSerie(Guid serieId) =>
        $"LastVideoTrackSelection:Serie:{serieId}";
}

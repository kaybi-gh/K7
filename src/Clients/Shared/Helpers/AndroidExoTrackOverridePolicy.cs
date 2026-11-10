namespace K7.Clients.Shared.Helpers;

/// <summary>
/// When the next episode reuses one ExoPlayer, <c>CurrentTracks</c> can still be the
/// previous item after the new URL is bound. A lock keyed only by URL then skips the
/// real groups, so the chosen subtitle stays selected in the UI and never renders.
/// </summary>
public static class AndroidExoTrackOverridePolicy
{
    public enum SubtitleBind
    {
        /// <summary>Groups were missing or belonged to another item. Try again.</summary>
        Pending,

        /// <summary>The text override was applied to the current groups.</summary>
        Applied,

        /// <summary>HLS sidecar is warming. Text stays off until the VTT is ready.</summary>
        Deferred,

        /// <summary>No text track should play.</summary>
        Cleared
    }

    public static bool MediaItemMatchesSource(string? mediaItemUri, string? sourceUrl)
    {
        if (string.IsNullOrEmpty(mediaItemUri) || string.IsNullOrEmpty(sourceUrl))
            return false;

        if (string.Equals(mediaItemUri, sourceUrl, StringComparison.Ordinal))
            return true;

        if (!Uri.TryCreate(mediaItemUri, UriKind.Absolute, out var media)
            || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out var source))
            return false;

        return Uri.Compare(
                media,
                source,
                UriComponents.HttpRequestUrl,
                UriFormat.SafeUnescaped,
                StringComparison.Ordinal)
            == 0;
    }

    /// <summary>
    /// True when the published groups are a new snapshot for this URL.
    /// An empty signature means the previous item was cleared and must not count
    /// as "already applied".
    /// </summary>
    public static bool ShouldApplyTrackOverrides(
        string? appliedUrl,
        string? appliedSignature,
        string url,
        string? currentSignature)
    {
        if (string.IsNullOrEmpty(currentSignature))
            return false;

        return !string.Equals(appliedUrl, url, StringComparison.Ordinal)
            || !string.Equals(appliedSignature, currentSignature, StringComparison.Ordinal);
    }

    public static bool ShouldRememberTrackOverride(SubtitleBind bind) =>
        bind is SubtitleBind.Applied or SubtitleBind.Deferred or SubtitleBind.Cleared;
}

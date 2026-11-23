namespace K7.Shared.Constants;

/// <summary>
/// Conventions of the sidecar WebVTT endpoint shared by the server and the clients.
/// </summary>
public static class SubtitleVttFormat
{
    /// <summary>
    /// NOTE block at the top of a VTT that only holds the cues around the requested position,
    /// served while the whole track is still being extracted. Carried in the body (a WebVTT
    /// comment, ignored by cue parsers) so it survives every transport: fetch, the Windows
    /// stream-fetch bridge and the federation proxy only forward status and body.
    /// </summary>
    public const string PartialNote = "NOTE k7-partial";

    private const int HeaderProbeLength = 64;

    /// <summary>
    /// True when <paramref name="vtt"/> is a partial answer: the client shows its cues and
    /// asks again until the server returns the complete track.
    /// </summary>
    public static bool IsPartial(string? vtt)
    {
        if (string.IsNullOrEmpty(vtt))
            return false;

        return vtt.AsSpan(0, Math.Min(vtt.Length, HeaderProbeLength))
            .Contains(PartialNote, StringComparison.Ordinal);
    }
}

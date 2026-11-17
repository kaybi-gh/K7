namespace K7.Shared.Enums;

/// <summary>
/// How the full screen music player shows the position in the track. Stored in preferences:
/// only add values at the end.
/// </summary>
public enum AudioSeekStyle
{
    /// <summary>The waveform of the track as a bar, or a plain bar when it has none. The default.</summary>
    Waveform,

    /// <summary>Always a plain bar.</summary>
    Bar,

    /// <summary>The waveform as a ring around a round cover.</summary>
    Ring,

    /// <summary>A plain progress ring around a round cover, without the waveform.</summary>
    PlainRing
}

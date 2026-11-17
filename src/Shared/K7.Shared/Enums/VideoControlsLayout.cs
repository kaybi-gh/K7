namespace K7.Shared.Enums;

/// <summary>
/// Layout of the bottom controls of the video player. Stored in preferences: only add values at the end.
/// </summary>
public enum VideoControlsLayout
{
    /// <summary>Buttons and progress bar on one line. The default.</summary>
    Inline,

    /// <summary>The buttons on one line, the progress bar on a line of its own under them.</summary>
    Stacked
}

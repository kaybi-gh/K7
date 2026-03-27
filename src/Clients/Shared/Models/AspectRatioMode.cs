namespace K7.Clients.Shared.Models;

/// <summary>
/// Defines how the video fits within the player viewport.
/// </summary>
public enum AspectRatioMode
{
    /// <summary>
    /// Fit the video within the container preserving aspect ratio (letterbox/pillarbox). Default.
    /// </summary>
    Fit,

    /// <summary>
    /// Fill the entire container, cropping the video if needed (zoom to fill).
    /// </summary>
    Fill,

    /// <summary>
    /// Stretch the video to fill the container, ignoring aspect ratio.
    /// </summary>
    Stretch
}

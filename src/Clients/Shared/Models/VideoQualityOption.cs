using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Models;

/// <summary>
/// Represents a selectable video quality option in the player overlay.
/// </summary>
public sealed record VideoQualityOption
{
    /// <summary>Display label shown to the user (e.g. "1080p", "720p", "Auto").</summary>
    public required string Label { get; init; }

    /// <summary>Vertical resolution in pixels. 0 for the "Auto" option.</summary>
    public required int Height { get; init; }

    /// <summary>True when this option represents the source file quality (transmux, no re-encode).</summary>
    public bool IsOriginal { get; init; }


    /// <summary>
    /// Builds the list of selectable quality options for a given source resolution.
    /// Returns "Original ({res})" + all standard resolutions strictly below the source, ordered descending by height.
    /// </summary>
    public static IReadOnlyList<VideoQualityOption> BuildOptionsForResolution(VideoResolutionIdentifier sourceResolution)
    {
        var sourceQuality = Constants.VideoQualities[sourceResolution];
        var options = new List<VideoQualityOption>
        {
            // Original (transmux at source quality)
            new() { Label = $"Original ({sourceQuality.Name})", Height = sourceQuality.Height, IsOriginal = true }
        };

        // Lower resolutions that require transcoding, ordered descending (e.g. 720p, 480p, 360p…)
        var lowerQualities = Constants.VideoQualities
            .Where(kvp => kvp.Value.Height < sourceQuality.Height)
            .OrderByDescending(kvp => kvp.Value.Height)
            .Select(kvp => new VideoQualityOption
            {
                Label = kvp.Value.Name,
                Height = kvp.Value.Height
            });

        options.AddRange(lowerQualities);

        return options;
    }
}

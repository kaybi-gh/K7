using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Common;

/// <summary>
/// Browsers and native players decode and scale locally. Display size must not
/// force a transcode. When an encode is already required, cap output to the
/// largest ladder rung that fits <see cref="Device.DisplayResolutionHeight"/>
/// (physical pixels). Logical screen size (CSS / DIP) is not used.
/// </summary>
public static class DisplayEncodeCap
{
    public static VideoResolution? TryGetEncodeQuality(Device device, int sourceHeight)
    {
        if (device.DisplayResolutionHeight <= 0 || sourceHeight <= 0)
            return null;

        if (sourceHeight <= device.DisplayResolutionHeight)
            return null;

        return Constants.VideoQualities.Values
            .Where(quality => quality.Height <= (int)device.DisplayResolutionHeight)
            .OrderByDescending(quality => quality.Height)
            .FirstOrDefault();
    }

    public static string ResolveJobQuality(string? requestedQuality, StreamDecisionDto? decision)
    {
        if (!string.IsNullOrEmpty(requestedQuality)
            && !string.Equals(requestedQuality, "original", StringComparison.OrdinalIgnoreCase))
        {
            return requestedQuality;
        }

        return TryGetQualityName(decision) ?? requestedQuality ?? "original";
    }

    public static string? TryGetQualityName(StreamDecisionDto? decision)
    {
        if (decision is null
            || !decision.Reason.HasFlag(TranscodeReason.QualityDownscale)
            || string.IsNullOrEmpty(decision.StreamResolution))
        {
            return null;
        }

        var parts = decision.StreamResolution.Split('x');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var width)
            || !int.TryParse(parts[1], out var height))
        {
            return null;
        }

        return Constants.VideoQualities.Values
            .FirstOrDefault(quality => quality.Width == width && quality.Height == height)
            ?.Name;
    }
}

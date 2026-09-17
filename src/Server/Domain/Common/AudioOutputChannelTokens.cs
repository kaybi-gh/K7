using System.Globalization;

namespace K7.Server.Domain.Common;

/// <summary>
/// Device audio output capability stored next to catalog format ids on
/// <c>DevicePlaybackCapabilities.SupportedMediaFormatIds</c> (like <see cref="VideoDecoderProfileTokens"/>).
/// <c>achannels:N</c> is the number of channels the device output can render
/// (Web: <c>AudioContext.destination.maxChannelCount</c>). Absent token: no cap
/// (legacy clients and natives that downmix themselves).
/// </summary>
public static class AudioOutputChannelTokens
{
    public const string Prefix = "achannels:";

    public static string MaxChannels(int channels) =>
        Prefix + channels.ToString(CultureInfo.InvariantCulture);

    public static int? TryReadMaxChannels(IEnumerable<string>? ids)
    {
        if (ids is null)
            return null;

        int? best = null;
        foreach (var id in ids)
        {
            if (!id.StartsWith(Prefix, StringComparison.Ordinal))
                continue;

            if (int.TryParse(id.AsSpan(Prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var channels)
                && channels > 0
                && (best is null || channels > best))
            {
                best = channels;
            }
        }

        return best;
    }
}

using System.Globalization;
using System.Text;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// The waveform of a track drawn as a ring of radial bars, for the "ring" skin of the full
/// screen music player. The ring starts at the top and runs clockwise.
/// </summary>
public static class WaveformRing
{
    public const int BarCount = 160;

    // In a 200 x 200 box: bars start outside the cover and never leave the box.
    private const double Center = 100;
    private const double InnerRadius = 83;
    private const double MinLength = 2.5;
    private const double MaxLength = 15;

    /// <summary>
    /// CSS mask (both spellings) showing the bars. Without peaks every bar has the same short
    /// length, so the ring still works as a progress ring.
    /// </summary>
    public static string MaskStyle(IReadOnlyList<float>? peaks)
    {
        var sb = new StringBuilder(BarCount * 40);
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>");
        sb.Append("<path fill='none' stroke='white' stroke-width='1.6' stroke-linecap='round' d='");
        for (var i = 0; i < BarCount; i++)
        {
            var angle = 2 * Math.PI * i / BarCount;
            var sin = Math.Sin(angle);
            var cos = Math.Cos(angle);
            var outer = InnerRadius + MinLength + BarLevel(peaks, i) * (MaxLength - MinLength);
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "M{0:F1},{1:F1} L{2:F1},{3:F1} ",
                Center + InnerRadius * sin,
                Center - InnerRadius * cos,
                Center + outer * sin,
                Center - outer * cos);
        }

        sb.Append("'/></svg>");
        var encoded = Uri.EscapeDataString(sb.ToString());
        return $"-webkit-mask-image: url(\"data:image/svg+xml,{encoded}\"); mask-image: url(\"data:image/svg+xml,{encoded}\");";
    }

    /// <summary>CSS mask (both spellings) of the plain ring: one thin circle, no waveform.</summary>
    public static string PlainMaskStyle()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>"
            + "<circle cx='100' cy='100' r='88' fill='none' stroke='white' stroke-width='5'/></svg>";
        var encoded = Uri.EscapeDataString(svg);
        return $"-webkit-mask-image: url(\"data:image/svg+xml,{encoded}\"); mask-image: url(\"data:image/svg+xml,{encoded}\");";
    }

    /// <summary>Level (0 to 1) of one bar: the loudest peak of the slice of the track it stands for.</summary>
    public static double BarLevel(IReadOnlyList<float>? peaks, int bar)
    {
        if (peaks is not { Count: > 0 })
            return 0;

        var from = (int)((long)bar * peaks.Count / BarCount);
        var to = Math.Max(from + 1, (int)((long)(bar + 1) * peaks.Count / BarCount));
        var level = 0f;
        for (var i = from; i < to && i < peaks.Count; i++)
            level = Math.Max(level, peaks[i]);

        return Math.Clamp(level, 0, 1);
    }

    /// <summary>
    /// Position in the track (0 to 1) for a pointer at (x, y) over a ring whose box is given:
    /// the angle from the top, clockwise.
    /// </summary>
    public static double FractionAt(double x, double y, double left, double top, double width, double height)
    {
        var dx = x - (left + width / 2);
        var dy = (top + height / 2) - y;
        var angle = Math.Atan2(dx, dy);
        if (angle < 0)
            angle += 2 * Math.PI;

        return Math.Clamp(angle / (2 * Math.PI), 0, 1);
    }
}

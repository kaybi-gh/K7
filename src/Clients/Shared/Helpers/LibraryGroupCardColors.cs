using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Clients.Shared.Helpers;

public static class LibraryGroupCardColors
{
    public static (string GradientStart, string GradientEnd, string IconColor) GetRgbaColors(LibraryGroupDto group) =>
        GetRgbaColors(group.MediaType, group.CardColor);

    public static (string GradientStart, string GradientEnd, string IconColor) GetRgbaColors(
        LibraryMediaType mediaType,
        string? cardColor) =>
        ToRgba(string.IsNullOrEmpty(cardColor) ? GetDefaultHex(mediaType) : cardColor);

    public static string GetDefaultHex(LibraryMediaType mediaType) => mediaType switch
    {
        LibraryMediaType.Movie => "#781e1e",
        LibraryMediaType.Serie => "#143c78",
        LibraryMediaType.Music => "#501464",
        _ => "#1e3c28"
    };

    public static (string GradientStart, string GradientEnd, string IconColor) ToRgba(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return (
            $"rgba({r},{g},{b},0.85)",
            $"rgba({Scale(r, 0.15)},{Scale(g, 0.15)},{Scale(b, 0.15)},0.9)",
            $"rgba({Boost(r)},{Boost(g)},{Boost(b)},0.6)");
    }

    private static int Scale(int channel, double factor) =>
        Math.Clamp((int)Math.Round(channel * factor), 0, 255);

    private static int Boost(int channel) =>
        Math.Clamp(channel + 60, 0, 255);

    private static (int R, int G, int B) ParseHex(string hex)
    {
        var normalized = hex.TrimStart('#');
        if (normalized.Length != 6)
            return (120, 30, 30);

        return (
            Convert.ToInt32(normalized[..2], 16),
            Convert.ToInt32(normalized[2..4], 16),
            Convert.ToInt32(normalized[4..6], 16));
    }
}

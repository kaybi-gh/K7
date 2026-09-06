namespace K7.Clients.Shared.Helpers;

/// <summary>
/// MAUI <c>DeviceDisplay</c> reports DIP in Width/Height. Physical pixels are
/// DIP * Density. Keep both on the device: screen = DIP, resolution = pixels.
/// </summary>
public static class DisplayPixelSize
{
    public static (double Width, double Height) FromDip(
        double dipWidth,
        double dipHeight,
        double density,
        bool landscape)
    {
        var scale = density > 0 ? density : 1;
        var pixelWidth = Math.Round(dipWidth * scale);
        var pixelHeight = Math.Round(dipHeight * scale);
        return landscape
            ? (pixelWidth, pixelHeight)
            : (pixelHeight, pixelWidth);
    }

    public static (double ScreenWidth, double ScreenHeight, double ResolutionWidth, double ResolutionHeight) FromDisplay(
        double dipWidth,
        double dipHeight,
        double density,
        bool landscape)
    {
        var screenWidth = landscape ? dipWidth : dipHeight;
        var screenHeight = landscape ? dipHeight : dipWidth;
        var (resolutionWidth, resolutionHeight) = FromDip(dipWidth, dipHeight, density, landscape);
        return (screenWidth, screenHeight, resolutionWidth, resolutionHeight);
    }
}

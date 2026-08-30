namespace K7.Clients.Shared.Helpers;

/// <summary>
/// MAUI <c>DeviceDisplay</c> reports DIP. Direct Play compared that to video
/// pixel height, so a 1080p panel at 150% scale looked like 720p.
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
}

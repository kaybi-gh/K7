namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Sprite-sheet seek preview math (matches Blazor SeekBar grid).
/// </summary>
public static class NativeSeekThumbnailHelper
{
    public const int ThumbWidth = 320;
    public const int ThumbHeight = 180;
    public const int IntervalSeconds = 30;
    public const int ThumbsPerRow = 10;

    public static int GetSpriteIndex(double timeSeconds)
    {
        if (timeSeconds < 0)
            timeSeconds = 0;
        return (int)(timeSeconds / IntervalSeconds);
    }

    public static (int Column, int Row) GetSpriteCell(double timeSeconds)
    {
        var index = GetSpriteIndex(timeSeconds);
        return (index % ThumbsPerRow, index / ThumbsPerRow);
    }

    public static (double TranslationX, double TranslationY, double SheetWidth, double SheetHeight) GetSpriteLayout(
        double timeSeconds,
        int estimatedRows = 20)
    {
        var (col, row) = GetSpriteCell(timeSeconds);
        var sheetWidth = ThumbsPerRow * ThumbWidth;
        var sheetHeight = Math.Max(estimatedRows, row + 1) * ThumbHeight;
        return (-col * ThumbWidth, -row * ThumbHeight, sheetWidth, sheetHeight);
    }
}

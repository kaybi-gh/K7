using System.Globalization;

namespace K7.Clients.Shared.UI.Components;

internal static class RatingStarValue
{
    public const int Max = 10;
    public const int StarCount = 5;

    public static int FromRatio(double ratio)
    {
        if (ratio <= 0)
            return 0;
        if (ratio >= 1)
            return Max;
        return Math.Min(Max, (int)(ratio * (Max + 1)));
    }

    public static string StarModifierClass(int star, int value)
    {
        var starStart = (star - 1) * 2;
        if (value <= starStart)
            return string.Empty;
        if (value >= starStart + 2)
            return "star--filled";
        return "star--half";
    }

    public static string FormatStarsLabel(int value)
    {
        if (value <= 0)
            return "0/5";
        return (value / 2.0).ToString("0.#", CultureInfo.InvariantCulture) + "/5";
    }
}

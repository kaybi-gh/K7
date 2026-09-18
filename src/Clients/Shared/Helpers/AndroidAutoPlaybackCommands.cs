using K7.Clients.Shared.Enums;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Android Auto session command ids and mappings for shuffle, repeat, and 0-5 stars.
/// K7 stores ratings as 0-10. Auto cycles full stars only (value = stars * 2).
/// </summary>
public static class AndroidAutoPlaybackCommands
{
    public const string Shuffle = "k7.shuffle";
    public const string Repeat = "k7.repeat";
    public const string Rate = "k7.rate";

    public const int RepeatOff = 0;
    public const int RepeatOne = 1;
    public const int RepeatAll = 2;
    public const int CommandSetShuffleMode = 14;
    public const int CommandSetRepeatMode = 15;

    public static int ToMedia3Repeat(RepeatMode mode) => mode switch
    {
        RepeatMode.One => RepeatOne,
        RepeatMode.All => RepeatAll,
        _ => RepeatOff
    };

    public static RepeatMode FromMedia3Repeat(int mode) => mode switch
    {
        RepeatOne => RepeatMode.One,
        RepeatAll => RepeatMode.All,
        _ => RepeatMode.Off
    };

    public static int ToStars(int value0to10)
    {
        if (value0to10 <= 0)
            return 0;
        return Math.Clamp(
            (int)Math.Round(value0to10 / 2.0, MidpointRounding.AwayFromZero),
            0,
            5);
    }

    public static int FromStars(int stars) => Math.Clamp(stars, 0, 5) * 2;

    public static int CycleValue(int value0to10)
    {
        var nextStars = (ToStars(value0to10) + 1) % 6;
        return FromStars(nextStars);
    }

    public static int ResolveValue(int? overlay, int? trackRating)
    {
        if (overlay.HasValue)
            return overlay.Value;
        return trackRating ?? 0;
    }
}

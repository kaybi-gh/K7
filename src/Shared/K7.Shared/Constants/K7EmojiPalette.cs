namespace K7.Shared.Constants;

public static class K7EmojiPalette
{
    public static readonly IReadOnlyList<string> All =
    [
        "\U0001F44D",
        "\U0001F602",
        "\U0001F525",
        "\u2764\uFE0F",
        "\U0001F62E",
        "\U0001F622",
        "\U0001F44F",
        "\U0001F389"
    ];

    public static bool IsAllowed(string? emoji) =>
        emoji is not null && All.Contains(emoji);
}

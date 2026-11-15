using System.Text.Json;

namespace K7.Shared.Theming;

/// <summary>
/// Validation and storage of media card preferences: the ones of a user, and the default card
/// style an administrator sets for accounts that chose nothing.
/// </summary>
public static class MediaCardStyles
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsValid(MediaCardPreferencesDto? cards) =>
        cards?.Overrides is not null
        && IsValid(cards.Default)
        && cards.Overrides.All(o => Enum.IsDefined(o.Key) && IsValid(o.Value));

    public static bool IsValid(MediaCardStyleDto? style) =>
        style?.Details is not null
        && style.Details.All(Enum.IsDefined)
        && style.Details.Distinct().Count() == style.Details.Count
        && Enum.IsDefined(style.DetailsPlacement)
        && Enum.IsDefined(style.Hover);

    public static string Serialize(MediaCardPreferencesDto cards) => JsonSerializer.Serialize(cards, _jsonOptions);

    /// <summary>Reads a stored default. Null when there is none, or when it is unreadable or not valid.</summary>
    public static MediaCardPreferencesDto? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var cards = JsonSerializer.Deserialize<MediaCardPreferencesDto>(json, _jsonOptions);
            return IsValid(cards) ? cards : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>An independent copy, safe to edit without touching the original.</summary>
    public static MediaCardPreferencesDto Copy(MediaCardPreferencesDto cards) => new()
    {
        Default = Copy(cards.Default),
        Overrides = cards.Overrides.ToDictionary(pair => pair.Key, pair => Copy(pair.Value))
    };

    public static MediaCardStyleDto Copy(MediaCardStyleDto style) => style with { Details = [.. style.Details] };
}

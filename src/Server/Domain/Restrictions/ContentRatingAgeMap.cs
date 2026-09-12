namespace K7.Server.Domain.Restrictions;

public static class ContentRatingAgeMap
{
    private static readonly Dictionary<string, int> MinimumAgeByNormalizedKey = new(StringComparer.Ordinal)
    {
        ["tp"] = 0,
        ["tous-publics"] = 0,
        ["u"] = 0,
        ["g"] = 0,
        ["tv-y"] = 0,
        ["tv-g"] = 0,
        ["0"] = 0,
        ["fsk-0"] = 0,
        ["tv-y7"] = 7,
        ["pg"] = 8,
        ["tv-pg"] = 10,
        ["10"] = 10,
        ["12"] = 12,
        ["12a"] = 12,
        ["fsk-12"] = 12,
        ["pg-13"] = 13,
        ["tv-14"] = 14,
        ["15"] = 15,
        ["ma-15"] = 15,
        ["16"] = 16,
        ["fsk-16"] = 16,
        ["r"] = 17,
        ["tv-ma"] = 17,
        ["18"] = 18,
        ["nc-17"] = 18,
        ["r-18"] = 18,
        ["x-18"] = 18,
        ["fsk-18"] = 18
    };

    public static int? TryGetMinimumAge(string normalizedKey) =>
        MinimumAgeByNormalizedKey.TryGetValue(normalizedKey, out var age) ? age : null;

    public static IReadOnlyList<string> GetAllowedNormalizedKeys(int viewerAge) =>
        [.. MinimumAgeByNormalizedKey.Where(entry => entry.Value <= viewerAge).Select(entry => entry.Key)];
}

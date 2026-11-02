using System.Globalization;

namespace K7.Server.Domain.Restrictions;

public static class ContentRatingAgeMap
{
    // Bare numeric labels (and zero-padded forms) map to that minimum age.
    // Display values like "13+" normalize to key "13" (non-alnum stripped).
    private const int MaxNumericAge = 21;

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

    public static int? TryGetMinimumAge(string normalizedKey)
    {
        if (string.IsNullOrEmpty(normalizedKey))
            return null;

        if (MinimumAgeByNormalizedKey.TryGetValue(normalizedKey, out var age))
            return age;

        return TryParseNumericAge(normalizedKey, out var numericAge) ? numericAge : null;
    }

    public static IReadOnlyList<string> GetAllowedNormalizedKeys(int viewerAge)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, minAge) in MinimumAgeByNormalizedKey)
        {
            if (minAge <= viewerAge)
                keys.Add(key);
        }

        var maxNumeric = Math.Min(Math.Max(viewerAge, 0), MaxNumericAge);
        for (var age = 0; age <= maxNumeric; age++)
        {
            keys.Add(age.ToString(CultureInfo.InvariantCulture));
            if (age <= 9)
                keys.Add(age.ToString("00", CultureInfo.InvariantCulture));
        }

        return [.. keys];
    }

    private static bool TryParseNumericAge(string normalizedKey, out int age)
    {
        age = 0;
        if (normalizedKey.Length is 0 or > 2)
            return false;

        foreach (var c in normalizedKey)
        {
            if (!char.IsAsciiDigit(c))
                return false;
        }

        if (!int.TryParse(normalizedKey, NumberStyles.None, CultureInfo.InvariantCulture, out age))
            return false;

        return age is >= 0 and <= MaxNumericAge;
    }
}

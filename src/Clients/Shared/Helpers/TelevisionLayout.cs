namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Couch / 10-foot device detection. Keep UA tokens in sync with <c>tv-layout.js</c>.
/// </summary>
public static class TelevisionLayout
{
    public const string UserAgentMarker = "K7TV/1.0";
    public const string FireTvFeature = "amazon.hardware.fire_tv";

    public static bool MatchesAndroidTelevision(
        bool uiModeTelevision,
        bool hasLeanback,
        bool hasFireTvFeature,
        string? model)
    {
        if (uiModeTelevision || hasLeanback || hasFireTvFeature)
            return true;

        return IsFireTvModel(model);
    }

    public static bool IsFireTvModel(string? model)
    {
        if (string.IsNullOrEmpty(model))
            return false;

        // Amazon Fire TV device codes start with AFT (AFTKA, AFTKM, AFTT, ...).
        return model.StartsWith("AFT", StringComparison.OrdinalIgnoreCase);
    }

    public static bool UserAgentLooksLikeTelevision(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return false;

        if (userAgent.Contains(UserAgentMarker, StringComparison.Ordinal))
            return true;

        if (userAgent.Contains("Android TV", StringComparison.OrdinalIgnoreCase))
            return true;

        return ContainsFireTvModelToken(userAgent);
    }

    private static bool ContainsFireTvModelToken(string userAgent)
    {
        var index = 0;
        while (index < userAgent.Length)
        {
            var found = userAgent.IndexOf("AFT", index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
                return false;

            if (found > 0 && IsTokenChar(userAgent[found - 1]))
            {
                index = found + 3;
                continue;
            }

            var after = found + 3;
            if (after < userAgent.Length && IsTokenChar(userAgent[after]))
                return true;

            index = found + 3;
        }

        return false;
    }

    private static bool IsTokenChar(char c) => char.IsLetterOrDigit(c);
}

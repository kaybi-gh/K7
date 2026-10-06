using System.Globalization;
using System.Text.RegularExpressions;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Query-string helper for the HLS master <c>AudioTrackTranscodings</c> parameter.
/// </summary>
public static class HlsManifestUrl
{
    public static string WithAudioTrackTranscodings(string baseUrl, IReadOnlyDictionary<int, string> transcodings)
    {
        var url = StripQueryKey(baseUrl, "AudioTrackTranscodings");
        if (transcodings.Count == 0)
            return url;

        var value = string.Join(
            ",",
            transcodings.OrderBy(kv => kv.Key)
                .Select(kv => $"{kv.Key.ToString(CultureInfo.InvariantCulture)}:{kv.Value}"));

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}AudioTrackTranscodings={Uri.EscapeDataString(value)}";
    }

    private static string StripQueryKey(string baseUrl, string key)
    {
        var pattern = $@"(?<=[?&]){Regex.Escape(key)}=[^&]*&?";
        var stripped = Regex.Replace(baseUrl, pattern, "", RegexOptions.IgnoreCase);
        return stripped.TrimEnd('?', '&');
    }
}

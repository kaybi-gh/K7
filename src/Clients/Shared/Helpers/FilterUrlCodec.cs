using System.Text;
using System.Text.Json;
using K7.Clients.Shared.Services;

namespace K7.Clients.Shared.Helpers;

public static class FilterUrlCodec
{
    private const int MaxEncodedLength = 4000;

    public static string? Encode<T>(T? value) where T : class
    {
        if (value is null)
            return null;

        var json = PageFilterJson.Serialize(value);
        var encoded = ToBase64Url(Encoding.UTF8.GetBytes(json));
        return encoded.Length <= MaxEncodedLength ? encoded : null;
    }

    public static T? Decode<T>(string? encoded) where T : class
    {
        if (string.IsNullOrWhiteSpace(encoded))
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(FromBase64Url(encoded));
            return PageFilterJson.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string ToBase64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string encoded)
    {
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        var mod = padded.Length % 4;
        padded = mod switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded
        };

        return Convert.FromBase64String(padded);
    }
}

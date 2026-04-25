using System.Data;

namespace K7.Shared.Extensions;

public static class QueryBuilderHelper
{
    public static string AddQueryParameters(string uri, IDictionary<string, string?>? queryParameters)
    {
        if (queryParameters == null)
        {
            return uri;
        }

        string queryString = string.Join("&", queryParameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        return string.IsNullOrEmpty(queryString) ? uri : $"{uri}?{queryString}";
    }

    public static string AddQueryParameters(string uri, IEnumerable<KeyValuePair<string, string?>> queryParameters)
    {
        var queryString = string.Join("&", queryParameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        return string.IsNullOrEmpty(queryString) ? uri : $"{uri}?{queryString}";
    }
}

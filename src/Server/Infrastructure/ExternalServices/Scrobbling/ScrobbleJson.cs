using System.Text.Json;

namespace K7.Server.Infrastructure.ExternalServices.Scrobbling;

internal static class ScrobbleJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);
}

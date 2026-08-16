using System.Text.Json;
using System.Xml.Linq;

namespace K7.Import.Sources.Plex;

internal readonly record struct PlexSwitchIdentity(
    string Token,
    string? Id,
    string? Title,
    string? Username);

internal static class PlexHomeAuth
{
    public static PlexSwitchIdentity? TryParseSwitchResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        if (body.TrimStart().StartsWith('{'))
            return TryParseSwitchJson(body);

        return TryParseSwitchXml(body);
    }

    public static string? TryParseServerAccessToken(string body, string machineIdentifier)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(machineIdentifier))
            return null;

        if (body.TrimStart().StartsWith('[') || body.TrimStart().StartsWith('{'))
            return TryParseResourcesJson(body, machineIdentifier);

        return TryParseResourcesXml(body, machineIdentifier);
    }

    public static bool IdentityMatches(
        PlexSwitchIdentity identity,
        string requestedId,
        string? requestedTitle,
        string? requestedUsername)
    {
        if (string.Equals(identity.Id, requestedId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(requestedTitle)
            && string.Equals(identity.Title, requestedTitle, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(requestedUsername)
            && string.Equals(identity.Username, requestedUsername, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static PlexSwitchIdentity? TryParseSwitchJson(string body)
    {
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        if (root.TryGetProperty("user", out var user))
            root = user;

        var token = ReadJsonString(root, "authToken")
            ?? ReadJsonString(root, "authenticationToken");
        if (string.IsNullOrWhiteSpace(token))
            return null;

        return new PlexSwitchIdentity(
            token,
            ReadJsonString(root, "id"),
            ReadJsonString(root, "title"),
            ReadJsonString(root, "username"));
    }

    private static PlexSwitchIdentity? TryParseSwitchXml(string body)
    {
        var xml = XDocument.Parse(body);
        var user = xml.Descendants().FirstOrDefault(e =>
            e.Attribute("authenticationToken") is not null
            || e.Attribute("authToken") is not null);
        if (user is null)
            return null;

        var token = (string?)user.Attribute("authenticationToken")
            ?? (string?)user.Attribute("authToken");
        if (string.IsNullOrWhiteSpace(token))
            return null;

        return new PlexSwitchIdentity(
            token,
            (string?)user.Attribute("id"),
            (string?)user.Attribute("title"),
            (string?)user.Attribute("username"));
    }

    private static string? TryParseResourcesXml(string body, string machineIdentifier)
    {
        var xml = XDocument.Parse(body);
        foreach (var device in xml.Descendants().Where(e => e.Name.LocalName is "Device"))
        {
            if (!IsServerDevice((string?)device.Attribute("provides"))
                || !string.Equals(
                    (string?)device.Attribute("clientIdentifier"),
                    machineIdentifier,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var token = (string?)device.Attribute("accessToken");
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }

        return null;
    }

    private static string? TryParseResourcesJson(string body, string machineIdentifier)
    {
        using var json = JsonDocument.Parse(body);
        foreach (var device in EnumerateResources(json.RootElement))
        {
            var provides = ReadJsonString(device, "provides");
            var clientId = ReadJsonString(device, "clientIdentifier");
            if (!IsServerDevice(provides)
                || !string.Equals(clientId, machineIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var token = ReadJsonString(device, "accessToken");
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }

        return null;
    }

    private static IEnumerable<JsonElement> EnumerateResources(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                yield return item;
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty("resources", out var resources)
            && resources.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resources.EnumerateArray())
                yield return item;
        }
    }

    private static bool IsServerDevice(string? provides) =>
        !string.IsNullOrWhiteSpace(provides)
        && provides.Contains("server", StringComparison.OrdinalIgnoreCase);

    private static string? ReadJsonString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

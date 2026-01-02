using K7.Shared.Dtos.Requests;

namespace K7.Shared.QueryBuilders;

public static class GetDeviceQueryUriBuilder
{
    public const string Route = "/api/devices/{id}";

    public static string Build(GetDeviceQuery query) => Route
        .Replace("{id}", $"{query.Id}");
}

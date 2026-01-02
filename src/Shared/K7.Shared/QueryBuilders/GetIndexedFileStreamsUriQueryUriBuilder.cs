using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;

namespace K7.Shared.QueryBuilders;

public static class GetIndexedFileStreamsUriQueryUriBuilder
{
    public const string Route = "/api/indexed-files/{id}/streams";

    public static string Build(GetIndexedFileStreamsUriQuery query)
    {
        var route = Route.Replace("{id}", $"{query.Id}");

        var queryParams = new Dictionary<string, string?>
        {
            { nameof(query.DeviceId), $"{query.DeviceId}" }
        };

        var filteredParams = queryParams
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .ToDictionary(x => x.Key, x => x.Value!);

        return filteredParams.Count > 0
            ? QueryBuilderHelper.AddQueryParameters(route, filteredParams!)
            : route;
    }
}

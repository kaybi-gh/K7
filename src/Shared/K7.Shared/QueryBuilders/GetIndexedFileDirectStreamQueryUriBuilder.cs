using K7.Shared.Dtos.Requests;

namespace K7.Shared.QueryBuilders;

public static class GetIndexedFileDirectStreamQueryUriBuilder
{
    public const string Route = "/api/indexed-files/{id}/direct-stream";

    public static string Build(GetIndexedFileDirectStreamQuery query) => Route
        .Replace("{id}", $"{query.Id}");

    public static string Build(Guid id) => Route
        .Replace("{id}", $"{id}");
}

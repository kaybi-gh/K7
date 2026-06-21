using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;

namespace K7.Shared.QueryBuilders;

public static class GetMediaBrowseFacetsQueryUriBuilder
{
    public const string Route = "/api/medias/browse-facets";

    public static string Build(GetMediaBrowseFacetsQuery query)
    {
        var queryParams = new List<KeyValuePair<string, string?>>();

        if (query.LibraryIds?.Length > 0)
        {
            foreach (var id in query.LibraryIds)
                queryParams.Add(new(nameof(query.LibraryIds), id.ToString()));
        }

        if (query.LibraryGroupIds?.Length > 0)
        {
            foreach (var id in query.LibraryGroupIds)
                queryParams.Add(new(nameof(query.LibraryGroupIds), id.ToString()));
        }

        if (query.MediaTypes?.Length > 0)
        {
            foreach (var mediaType in query.MediaTypes)
                queryParams.Add(new(nameof(query.MediaTypes), mediaType.ToString()));
        }

        return QueryBuilderHelper.AddQueryParameters(Route, queryParams);
    }
}

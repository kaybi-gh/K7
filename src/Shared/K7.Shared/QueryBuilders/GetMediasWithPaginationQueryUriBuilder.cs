using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;

namespace K7.Shared.QueryBuilders;

// TODO - Move to a dedicated directory // Entity.Query.Builder?
public static class GetMediasWithPaginationQueryUriBuilder
{
    public const string Route = "/api/medias";

    public static string Build(GetMediasWithPaginationQuery query)
    {
        var queryParams = new List<KeyValuePair<string, string?>>
        {
            new(nameof(query.PageNumber), $"{query.PageNumber}"),
            new(nameof(query.PageSize), $"{query.PageSize}")
        };

        if (query.LibraryIds?.Length > 0)
        {
            foreach (var id in query.LibraryIds)
                queryParams.Add(new(nameof(query.LibraryIds), id.ToString()));
        }

        if (query.Ids?.Length > 0)
        {
            foreach (var id in query.Ids)
                queryParams.Add(new(nameof(query.Ids), id.ToString()));
        }

        if (query.MediaTypes?.Count > 0)
        {
            foreach (var mt in query.MediaTypes)
                queryParams.Add(new(nameof(query.MediaTypes), mt.ToString()));
        }

        if (query.OrderBy?.Count > 0)
        {
            foreach (var o in query.OrderBy)
                queryParams.Add(new(nameof(query.OrderBy), o.ToString()));
        }

        if (query.ContinueWatching.HasValue)
        {
            queryParams.Add(new(nameof(query.ContinueWatching), $"{query.ContinueWatching.Value}"));
        }

        if (query.PersonIds?.Length > 0)
        {
            foreach (var id in query.PersonIds)
                queryParams.Add(new(nameof(query.PersonIds), id.ToString()));
        }

        if (query.Genres?.Length > 0)
        {
            foreach (var g in query.Genres)
                queryParams.Add(new(nameof(query.Genres), g));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            queryParams.Add(new(nameof(query.SearchText), query.SearchText));
        }

        return QueryBuilderHelper.AddQueryParameters(Route, queryParams);
    }
}

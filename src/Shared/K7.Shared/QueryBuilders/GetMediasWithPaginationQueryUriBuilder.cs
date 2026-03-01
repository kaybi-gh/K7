using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;

namespace K7.Shared.QueryBuilders;

// TODO - Move to a dedicated directory // Entity.Query.Builder?
public static class GetMediasWithPaginationQueryUriBuilder
{
    public const string Route = "/api/medias";

    public static string Build(GetMediasWithPaginationQuery query)
    {
        var queryParams = new Dictionary<string, string?>
        {
            { nameof(query.PageNumber), $"{query.PageNumber}" },
            { nameof(query.PageSize), $"{query.PageSize}" }
        };

        if (query.LibraryIds?.Length > 0)
        {
            queryParams.Add(nameof(query.LibraryIds), string.Join(",", query.LibraryIds));
        }

        if (query.Ids?.Length > 0)
        {
            queryParams.Add(nameof(query.LibraryIds), string.Join(",", query.Ids));
        }

        if (query.MediaTypes?.Count > 0)
        {
            queryParams.Add(nameof(query.MediaTypes), string.Join(",", query.MediaTypes));
        }

        if (query.OrderBy?.Count > 0)
        {
            queryParams.Add(nameof(query.OrderBy), string.Join(",", query.OrderBy));
        }

        if (query.ContinueWatching.HasValue)
        {
            queryParams.Add(nameof(query.ContinueWatching), $"{query.ContinueWatching.Value}");
        }

        return QueryBuilderHelper.AddQueryParameters(Route, queryParams);
    }
}

using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;

namespace K7.Shared.QueryBuilders;

public static class GetMediaGenresQueryUriBuilder
{
    public const string Route = "/api/medias/genres";

    public static string Build(GetMediaGenresQuery query)
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

        if (query.LibraryGroupIds?.Length > 0)
        {
            foreach (var id in query.LibraryGroupIds)
                queryParams.Add(new(nameof(query.LibraryGroupIds), id.ToString()));
        }

        if (query.MediaTypes?.Count > 0)
        {
            foreach (var mt in query.MediaTypes)
                queryParams.Add(new(nameof(query.MediaTypes), mt.ToString()));
        }

        if (query.UnwatchedOnly.HasValue)
            queryParams.Add(new(nameof(query.UnwatchedOnly), $"{query.UnwatchedOnly.Value}"));

        if (query.OrderBy?.Count > 0)
        {
            foreach (var o in query.OrderBy)
                queryParams.Add(new(nameof(query.OrderBy), o.ToString()));
        }

        return QueryBuilderHelper.AddQueryParameters(Route, queryParams);
    }
}

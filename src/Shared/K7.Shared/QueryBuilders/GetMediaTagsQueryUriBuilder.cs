using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;

namespace K7.Shared.QueryBuilders;

public static class GetMediaTagsQueryUriBuilder
{
    public const string Route = "/api/medias/tags";

    public static string Build(GetMediaTagsQuery query)
    {
        var queryParams = new List<KeyValuePair<string, string?>>
        {
            new(nameof(query.Limit), query.Limit.ToString()),
            new(nameof(query.PageNumber), query.PageNumber.ToString()),
            new(nameof(query.PageSize), query.PageSize.ToString())
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

        if (query.MediaTypes?.Length > 0)
        {
            foreach (var mediaType in query.MediaTypes)
                queryParams.Add(new(nameof(query.MediaTypes), mediaType.ToString()));
        }

        if (query.Kinds?.Length > 0)
        {
            foreach (var kind in query.Kinds)
                queryParams.Add(new(nameof(query.Kinds), kind.ToString()));
        }

        if (query.OrderBy?.Length > 0)
        {
            foreach (var order in query.OrderBy)
                queryParams.Add(new(nameof(query.OrderBy), order.ToString()));
        }

        if (query.UnwatchedOnly == true)
            queryParams.Add(new(nameof(query.UnwatchedOnly), bool.TrueString));

        if (!string.IsNullOrWhiteSpace(query.SearchText))
            queryParams.Add(new(nameof(query.SearchText), query.SearchText.Trim()));

        return QueryBuilderHelper.AddQueryParameters(Route, queryParams);
    }
}

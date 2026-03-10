using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;

namespace K7.Shared.QueryBuilders;

public static class GetPersonsWithPaginationQueryUriBuilder
{
    public const string Route = "/api/persons";

    public static string Build(GetPersonsWithPaginationQuery query)
    {
        var queryParams = new Dictionary<string, string?>
        {
            { nameof(query.PageNumber), $"{query.PageNumber}" },
            { nameof(query.PageSize), $"{query.PageSize}" }
        };

        if (query.Ids?.Length > 0)
        {
            queryParams.Add(nameof(query.Ids), string.Join(",", query.Ids));
        }

        if (query.MediaIds?.Length > 0)
        {
            queryParams.Add(nameof(query.MediaIds), string.Join(",", query.MediaIds));
        }

        if (query.RoleTypes?.Count > 0)
        {
            queryParams.Add(nameof(query.RoleTypes), string.Join(",", query.RoleTypes));
        }

        return QueryBuilderHelper.AddQueryParameters(Route, queryParams);
    }
}

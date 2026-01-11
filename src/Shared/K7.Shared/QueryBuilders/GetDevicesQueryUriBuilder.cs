using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;

namespace K7.Shared.QueryBuilders;

public static class GetDevicesQueryUriBuilder
{
    public const string Route = "/api/devices";

    public static string Build(GetDevicesQuery? query)
    {
        if (query == null)
        {
            query = new();
        }

        var queryParams = new Dictionary<string, string?>
        {
            { nameof(query.PageNumber), $"{query.PageNumber}" },
            { nameof(query.PageSize), $"{query.PageSize}" }
        };

        if (query.Ids?.Length > 0)
        {
            queryParams.Add(nameof(query.Ids), string.Join(",", query.Ids));
        }

        if (query.UserIds?.Length > 0)
        {
            queryParams.Add(nameof(query.UserIds), string.Join(",", query.UserIds));
        }

        if (query.ClientTypes?.Count > 0)
        {
            queryParams.Add(nameof(query.ClientTypes), string.Join(",", query.ClientTypes));
        }

        if (query.DeviceTypes?.Count > 0)
        {
            queryParams.Add(nameof(query.DeviceTypes), string.Join(",", query.DeviceTypes));
        }

        if (query.OperatingSystems?.Count > 0)
        {
            queryParams.Add(nameof(query.OperatingSystems), string.Join(",", query.OperatingSystems));
        }

        if (query.OrderBy?.Count > 0)
        {
            queryParams.Add(nameof(query.OrderBy), string.Join(",", query.OrderBy));
        }

        return QueryBuilderHelper.AddQueryParameters(Route, queryParams);
    }
}

using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Medias.Queries.GetMediaBrowseFilterSuggestions;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class GetMediaBrowseFilterSuggestions : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(GetMediaBrowseFilterSuggestionsQueryUriBuilder.Route, async (
            [FromServices] ISender sender,
            [AsParameters] K7.Shared.Dtos.Requests.GetMediaBrowseFilterSuggestionsQuery query) =>
        {
            var applicationQuery = new Application.Features.Medias.Queries.GetMediaBrowseFilterSuggestions.GetMediaBrowseFilterSuggestionsQuery
            {
                LibraryIds = query.LibraryIds,
                LibraryGroupIds = query.LibraryGroupIds,
                MediaTypes = ToEnumHashSet(query.MediaTypes),
                Field = query.Field,
                SearchText = query.SearchText,
                Limit = query.Limit
            };

            var result = await sender.Send(applicationQuery);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static EnumHashSetQueryParam<MediaType>? ToEnumHashSet(MediaType[]? values)
    {
        if (values is not { Length: > 0 })
            return null;

        var result = new EnumHashSetQueryParam<MediaType>();
        foreach (var value in values)
            result.Add(value);
        return result;
    }
}

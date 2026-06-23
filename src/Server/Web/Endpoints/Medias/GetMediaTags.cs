using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class GetMediaTags : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(GetMediaTagsQueryUriBuilder.Route, async (
            [FromServices] ISender sender,
            [AsParameters] GetMediaTagsQuery query) =>
        {
            var applicationQuery = new Application.Features.Medias.Queries.GetMediaTags.GetMediaTagsQuery
            {
                LibraryIds = query.LibraryIds,
                LibraryGroupIds = query.LibraryGroupIds,
                MediaTypes = ToEnumHashSet(query.MediaTypes),
                Kinds = ToEnumHashSet(query.Kinds),
                SearchText = query.SearchText,
                UnwatchedOnly = query.UnwatchedOnly,
                OrderBy = ToEnumHashSet(query.OrderBy),
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Limit = query.Limit
            };

            var result = await sender.Send(applicationQuery);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static EnumHashSetQueryParam<TEnum>? ToEnumHashSet<TEnum>(TEnum[]? values)
        where TEnum : struct, Enum
    {
        if (values is not { Length: > 0 })
            return null;

        var result = new EnumHashSetQueryParam<TEnum>();
        foreach (var value in values)
            result.Add(value);
        return result;
    }
}

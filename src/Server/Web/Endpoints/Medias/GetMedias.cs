using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Medias.Queries.GetMedias;
using K7.Server.Domain.Constants;
using K7.Server.Web.Converters;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Medias;

public class GetMedias : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(GetMediasWithPaginationQueryUriBuilder.Route, async ([FromServices] ISender sender, [AsParameters] GetMediasWithPaginationQuery query) =>
        {
            var mediasPage = await sender.Send(query);
            return mediasPage.ToDto(m => m.ToLiteMediaDto());
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

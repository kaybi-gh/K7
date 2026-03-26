using K7.Server.Application.Features.Restrictions.Queries.PreviewRestrictedMedias;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Restrictions;

public class PreviewRestrictedMedias : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/restriction-profiles/{id:guid}/restricted-medias", async (
            [FromRoute] Guid id,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var medias = await sender.Send(new PreviewRestrictedMediasQuery(id), cancellationToken);
            return Results.Ok(medias);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

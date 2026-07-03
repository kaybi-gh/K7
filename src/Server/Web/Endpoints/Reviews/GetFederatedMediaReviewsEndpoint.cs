using K7.Server.Application.Features.Federation.Queries.GetFederatedMediaReviews;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Reviews;

public class GetFederatedMediaReviewsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/medias/{mediaId:guid}/federated-reviews", async (
            Guid mediaId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var reviews = await sender.Send(new GetFederatedMediaReviewsQuery(mediaId), cancellationToken);
            return Results.Ok(reviews);
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

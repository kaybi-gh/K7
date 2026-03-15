using K7.Server.Application.Features.MediaFormatSample.Queries.GetMediaFormatSample;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

// TODO - Remove?
public class GetMediaFormatSample : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/media-formats/{id}/sample", async ([FromServices] ISender sender, string id, CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetMediaFormatSampleQuery(id), cancellationToken);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

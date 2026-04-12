using K7.Server.Application.Features.Admin.Queries.GetActiveStreams;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class GetActiveStreams : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/admin/streams", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetActiveStreamsQuery(), cancellationToken);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

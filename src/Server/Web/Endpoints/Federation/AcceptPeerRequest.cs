using K7.Server.Application.Features.Federation.Commands.AcceptPeerRequest;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class AcceptPeerRequestEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/peers/requests/{id:guid}/accept", async (
            Guid id,
            [FromBody] AcceptPeerRequestRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new AcceptPeerRequestCommand
            {
                RequestId = id,
                SharedLibraryIds = request.SharedLibraryIds,
                AutoShareNewLibraries = request.AutoShareNewLibraries,
                MaxConcurrentStreams = request.MaxConcurrentStreams
            }, cancellationToken);

            return Results.Ok();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

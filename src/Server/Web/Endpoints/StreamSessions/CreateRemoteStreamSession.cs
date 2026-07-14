using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Commands.CreateRemoteStreamSession;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.StreamSessions;

public class CreateRemoteStreamSession : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/remote-stream-sessions", async (
            [FromBody] CreateRemoteStreamSessionRequest request,
            [FromServices] ISender sender,
            [FromServices] IUser user,
            CancellationToken cancellationToken) =>
        {
            if (user.Id is not { } userId)
                return Results.Unauthorized();

            var result = await sender.Send(new CreateRemoteStreamSessionCommand(request, userId), cancellationToken);
            return Results.Created(result.Location, result.Session);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

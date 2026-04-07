using K7.Server.Application.Features.Diagnostics.Commands.FixDiagnosticItems;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Diagnostics;

public class FixDiagnosticItems : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/diagnostics/fix", async (
            [FromServices] ISender sender,
            [FromBody] FixDiagnosticItemsRequest request,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new FixDiagnosticItemsCommand
            {
                EntityIds = request.EntityIds,
                Action = request.Action
            }, cancellationToken);

            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

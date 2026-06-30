using K7.Server.Application.Features.Diagnostics.Commands.QueueDiagnosticFixes;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Diagnostics;

public class QueueDiagnosticFixes : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/diagnostics/queue-fixes", async (
            [FromServices] ISender sender,
            [FromBody] QueueDiagnosticFixesRequest request,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new QueueDiagnosticFixesCommand
            {
                Issue = request.Issue,
                LibraryId = request.LibraryId
            }, cancellationToken);

            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

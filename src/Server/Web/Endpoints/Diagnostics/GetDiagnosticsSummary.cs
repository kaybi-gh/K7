using K7.Server.Application.Features.Diagnostics.Queries.GetDiagnosticsSummary;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Diagnostics;

public class GetDiagnosticsSummary : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/diagnostics/summary", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetDiagnosticsSummaryQuery(), cancellationToken);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

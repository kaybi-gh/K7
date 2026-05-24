using K7.Server.Application.Features.Libraries.Queries.GetLibraryStatistics;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Libraries;

public class GetLibraryStatistics : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/libraries/statistics", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetLibraryStatisticsQuery(), cancellationToken);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

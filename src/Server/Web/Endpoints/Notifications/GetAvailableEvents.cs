using K7.Server.Application.Features.Notifications.Queries.GetAvailableEvents;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Notifications;

public class GetAvailableEvents : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/notifications/events", async ([FromServices] ISender sender, CancellationToken cancellationToken) =>
        {
            return await sender.Send(new GetAvailableEventsQuery(), cancellationToken);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

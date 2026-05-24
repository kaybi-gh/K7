using K7.Server.Application.Features.Restrictions.Commands.CreateContentRestrictionProfile;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Restrictions;

public class CreateContentRestrictionProfile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/restriction-profiles", async (
            [FromBody] CreateContentRestrictionProfileRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var id = await sender.Send(new CreateContentRestrictionProfileCommand
            {
                Name = request.Name,
                Description = request.Description,
                RuleFilter = request.RuleFilter
            }, cancellationToken);

            return Results.Created($"/api/restriction-profiles/{id}", id);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

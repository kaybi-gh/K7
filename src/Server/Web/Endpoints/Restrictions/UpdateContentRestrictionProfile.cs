using K7.Server.Application.Features.Restrictions.Commands.UpdateContentRestrictionProfile;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Restrictions;

public class UpdateContentRestrictionProfile : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/restriction-profiles/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateContentRestrictionProfileRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdateContentRestrictionProfileCommand
            {
                Id = id,
                Name = request.Name,
                Description = request.Description,
                MatchCondition = request.MatchCondition,
                Rules = request.Rules.Select(r => new ContentRestrictionRuleCommand
                {
                    Field = r.Field,
                    Operator = r.Operator,
                    Value = r.Value
                }).ToList()
            }, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

using K7.Server.Application.Features.PasswordPolicySettings.Commands.UpdatePasswordPolicy;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdatePasswordPolicy : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/admin/password-policy", async (
            [FromBody] PasswordPolicyDto policy,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var updated = await sender.Send(new UpdatePasswordPolicyCommand { Policy = policy }, cancellationToken);
            return Results.Ok(updated);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

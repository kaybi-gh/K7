using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class GetFederationSocialPolicy : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/admin/federation/social-policy", async (
            [FromServices] IFederationSocialPolicyService policyService,
            CancellationToken cancellationToken) =>
        {
            var policy = await policyService.GetAsync(cancellationToken);
            return Results.Ok(policy);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class UpdateFederationSocialPolicy : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/admin/federation/social-policy", async (
            [FromBody] FederationSocialPolicyDto policy,
            [FromServices] IFederationSocialPolicyService policyService,
            CancellationToken cancellationToken) =>
        {
            await policyService.SetAsync(policy, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

using K7.Server.Application.Features.Users.Queries.GetLoginMethods;
using K7.Server.Domain.Constants;
using K7.Server.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.Endpoints.Users;

public class GetLoginMethods : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/login-methods", async (
            [FromServices] ISender sender,
            [FromServices] IOptions<AuthenticationConfiguration> authConfig,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetLoginMethodsQuery(), cancellationToken);
            var oidc = authConfig.Value.Oidc;
            var hasOidcLogin = result.ExternalLogins.Any(l =>
                string.Equals(l.Provider, "oidc", StringComparison.OrdinalIgnoreCase));

            return Results.Ok(result with
            {
                CanLinkOidc = oidc.Enabled && !hasOidcLogin,
                OidcDisplayName = oidc.Enabled ? oidc.DisplayName : null
            });
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

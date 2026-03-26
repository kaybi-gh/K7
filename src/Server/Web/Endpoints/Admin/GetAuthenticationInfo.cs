using K7.Server.Domain.Constants;
using K7.Server.Infrastructure.Configuration;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.Endpoints.Admin;

public class GetAuthenticationInfo : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/admin/authentication-info", (
            [FromServices] IOptions<AuthenticationConfiguration> authConfig) =>
        {
            var auth = authConfig.Value;
            return Results.Ok(new AuthenticationInfoDto
            {
                LocalSignInEnabled = auth.Local.SignInEnabled,
                LocalRegistrationEnabled = auth.Local.RegistrationEnabled,
                OidcEnabled = auth.Oidc.Enabled,
                OidcDisplayName = auth.Oidc.Enabled ? auth.Oidc.DisplayName : null,
                OidcAutomaticAccountCreation = auth.Oidc.AutomaticAccountCreation,
            });
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

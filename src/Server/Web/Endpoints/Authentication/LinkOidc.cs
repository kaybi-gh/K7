using K7.Server.Infrastructure.Configuration;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.Endpoints.Authentication;

public class LinkOidc : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/authentication/link", (
            [FromQuery] string? returnUrl,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IOptions<AuthenticationConfiguration> authConfig) =>
        {
            if (!authConfig.Value.Oidc.Enabled)
                return Results.Redirect(OidcLinkHelper.BuildResultUrl(returnUrl, "disabled"));

            var destination = OidcLinkHelper.BuildPendingUrl(returnUrl);
            var properties = signInManager.ConfigureExternalAuthenticationProperties("oidc", destination);
            properties.Items[OidcLinkHelper.LinkMarkerKey] = OidcLinkHelper.LinkMarkerValue;
            return Results.Challenge(properties, ["oidc"]);
        })
        .RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

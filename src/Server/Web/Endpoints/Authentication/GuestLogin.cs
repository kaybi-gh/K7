using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace K7.Server.Web.Endpoints.Authentication;

public class GuestLogin : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/authentication/guest-login", async (
            HttpContext httpContext,
            [FromServices] IServerSettingsService settingsService,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] IOpenIddictApplicationManager applicationManager,
            [FromServices] IOpenIddictAuthorizationManager authorizationManager,
            [FromServices] IOpenIddictScopeManager scopeManager,
            CancellationToken cancellationToken) =>
        {
            var guestEnabled = await settingsService.GetAsync(ServerSettingKeys.GuestEnabled, cancellationToken) == true;
            if (!guestEnabled)
                return Results.Forbid();

            var guestUser = await userManager.FindByNameAsync(Roles.Guest);
            if (guestUser is null)
                return Results.NotFound("Guest user not found.");

            var roles = await userManager.GetRolesAsync(guestUser);

            var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
            identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(guestUser))
                    .SetClaim(Claims.Name, await userManager.GetUserNameAsync(guestUser))
                    .SetClaims(Claims.Role, [.. roles]);

            identity.SetScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, "api");
            identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

            var application = await applicationManager.FindByClientIdAsync("k7-native");
            if (application is not null)
            {
                var authorization = await authorizationManager.CreateAsync(
                    identity: identity,
                    subject: await userManager.GetUserIdAsync(guestUser),
                    client: (await applicationManager.GetIdAsync(application))!,
                    type: AuthorizationTypes.Permanent,
                    scopes: identity.GetScopes());

                identity.SetAuthorizationId(await authorizationManager.GetIdAsync(authorization));
            }

            identity.SetDestinations(claim => claim.Type switch
            {
                Claims.Name or Claims.PreferredUsername or Claims.Email
                    => [Destinations.AccessToken, Destinations.IdentityToken],
                Claims.Role
                    => [Destinations.AccessToken, Destinations.IdentityToken],
                "AspNet.Identity.SecurityStamp" => [],
                _ => [Destinations.AccessToken]
            });

            return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        })
        .AllowAnonymous()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

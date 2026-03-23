using System.Security.Claims;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace K7.Server.Web.Endpoints.Connect;

public class Authorize : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], async (HttpContext httpContext,
            [FromServices] IOpenIddictApplicationManager applicationManager,
            [FromServices] IOpenIddictAuthorizationManager authorizationManager,
            [FromServices] IOpenIddictScopeManager scopeManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] IApplicationDbContext dbContext) =>
        {
            var request = httpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
            
            var result = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (result is null || !result.Succeeded || request.HasPromptValue(PromptValues.Login))
            {
                var currentUrl = httpContext.Request.PathBase + httpContext.Request.Path + httpContext.Request.QueryString;
                return Results.Challenge(new AuthenticationProperties { RedirectUri = currentUrl });
            }

            var user = await userManager.GetUserAsync(result.Principal) ??
                throw new InvalidOperationException("The user details cannot be retrieved.");

            var domainUser = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdentityUserId == user.Id, httpContext.RequestAborted);

            if (domainUser is not null && !domainUser.IsActive)
            {
                return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user account has been deactivated."
                }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            var application = await applicationManager.FindByClientIdAsync(request.ClientId!) ??
                throw new InvalidOperationException("Application not found.");

            var authorizations = await authorizationManager.FindAsync(
                subject: await userManager.GetUserIdAsync(user),
                client: await applicationManager.GetIdAsync(application),
                status: Statuses.Valid,
                type: AuthorizationTypes.Permanent,
                scopes: request.GetScopes()).ToListAsync();

            var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
            identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user))
                    .SetClaim(Claims.Email, await userManager.GetEmailAsync(user))
                    .SetClaim(Claims.Name, await userManager.GetUserNameAsync(user))
                    .SetClaim(Claims.PreferredUsername, await userManager.GetUserNameAsync(user))
                    .SetClaims(Claims.Role, [.. (await userManager.GetRolesAsync(user))]);

            identity.SetScopes(request.GetScopes());
            identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

            identity.SetDestinations(claim => claim.Type switch
            {
                Claims.Name or Claims.PreferredUsername or Claims.Email
                    => [Destinations.AccessToken, Destinations.IdentityToken],

                Claims.Role
                    => [Destinations.AccessToken, Destinations.IdentityToken],

                "AspNet.Identity.SecurityStamp" => [],

                _ => [Destinations.AccessToken]
            });

            // Automatically create a permanent authorization to avoid requiring explicit consent
            // for future authorization or token requests containing the same scopes.
            var authorization = authorizations.LastOrDefault();
            authorization ??= await authorizationManager.CreateAsync(
                identity: identity,
                subject: await userManager.GetUserIdAsync(user),
                client: (await applicationManager.GetIdAsync(application))!,
                type: AuthorizationTypes.Permanent,
                scopes: identity.GetScopes());

            identity.SetAuthorizationId(await authorizationManager.GetIdAsync(authorization));

            return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        })
        .WithName(type.Name)
        .WithTags(groupName);
        //.WithOpenApi(); // TODO - Make different name if openApi is required
    }
}

public static class AsyncEnumerableExtensions
{
    public static Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return ExecuteAsync();

        async Task<List<T>> ExecuteAsync()
        {
            var list = new List<T>();

            await foreach (var element in source)
            {
                list.Add(element);
            }

            return list;
        }
    }
}

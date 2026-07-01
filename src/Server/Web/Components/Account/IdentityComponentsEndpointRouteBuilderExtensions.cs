using System.Security.Claims;
using K7.Server.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using K7.Server.Infrastructure.Database.Context.Identity;

namespace K7.Server.Web.Components.Account
{
    internal static class IdentityComponentsEndpointRouteBuilderExtensions
    {
        public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var accountGroup = endpoints.MapGroup("/account");

            accountGroup.MapPost("/performsetupexternallogin", (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider) =>
            {
                IEnumerable<KeyValuePair<string, StringValues>> query = [
                    new("Action", "SetupCallback")];

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/setup/external-login",
                    QueryString.Create(query));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return TypedResults.Challenge(properties, [provider]);
            });

            accountGroup.MapGet("/autologin", (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromServices] IOptions<AuthenticationConfiguration> authConfig) =>
            {
                var auth = authConfig.Value;
                if (auth.Local.SignInEnabled || !auth.Oidc.Enabled)
                {
                    return Results.Redirect("/sign-in");
                }

                return Results.Redirect("/api/authentication/login?returnUrl=/");
            });

            accountGroup.MapMethods("/logout", [HttpMethods.Get, HttpMethods.Post], async (
                ClaimsPrincipal user,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromQuery] string returnUrl = "") =>
            {
                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect($"~/{returnUrl}");
            });

            return accountGroup;
        }
    }
}

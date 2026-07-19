using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using K7.Server.Infrastructure.Configuration;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.Endpoints.Authentication;

public class LogIn : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/authentication/login", (
            [FromQuery] string? returnUrl,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IOptions<AuthenticationConfiguration> authConfig) =>
        {
            // Cookie middleware uses ReturnUrl; clients may send returnUrl.
            if (string.IsNullOrWhiteSpace(returnUrl))
                returnUrl = null;

            if (!authConfig.Value.Oidc.Enabled)
            {
                var signInTarget = string.IsNullOrWhiteSpace(returnUrl)
                    ? "/sign-in"
                    : $"/sign-in?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
                return Results.Redirect(signInTarget);
            }

            var destination = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
            var properties = signInManager.ConfigureExternalAuthenticationProperties("oidc", destination);
            return Results.Challenge(properties, ["oidc"]);
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

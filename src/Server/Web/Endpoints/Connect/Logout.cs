using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;

namespace K7.Server.Web.Endpoints.Connect;

public class Logout : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/connect/logout", async ([FromServices] SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.SignOut(authenticationSchemes: [
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                IdentityConstants.ApplicationScheme,
                IdentityConstants.ExternalScheme]);
        })
        //.RequireAuthorization()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Client.AspNetCore;

namespace K7.Server.Web.Endpoints.Authentication;

public class LogIn : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/authentication/login", ([FromQuery] string returnUrl, CancellationToken cancellationToken) =>
        {
            return Results.Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? "/" });
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

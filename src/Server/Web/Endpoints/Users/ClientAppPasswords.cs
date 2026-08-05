using K7.Server.Application.Features.ClientAppPasswords.Commands.CreateClientAppPassword;
using K7.Server.Application.Features.ClientAppPasswords.Commands.RevokeClientAppPassword;
using K7.Server.Application.Features.ClientAppPasswords.Queries.GetClientAppPasswords;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class GetClientAppPasswords : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/client-app-passwords", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await sender.Send(new GetClientAppPasswordsQuery(), cancellationToken));
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class CreateClientAppPassword : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/users/me/client-app-passwords", async (
            [FromBody] CreateClientAppPasswordCommand command,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(command, cancellationToken);
            return Results.Created($"/api/users/me/client-app-passwords/{result.Id}", result);
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public class RevokeClientAppPassword : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapDelete("/api/users/me/client-app-passwords/{id:guid}", async (
            Guid id,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new RevokeClientAppPasswordCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.UserOrAbove)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

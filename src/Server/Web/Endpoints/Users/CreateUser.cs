using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Users.Commands.CreateUser;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class CreateUser : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/users", async (
            [FromBody] CreateUserRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var userDto = await sender.Send(new CreateUserCommand
            {
                Username = request.Username,
                Role = request.Role,
                Password = request.Password
            }, cancellationToken);
            return Results.Created($"/api/users/{userDto.Id}", userDto);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

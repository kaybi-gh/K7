using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Commands.InitiatePeering;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class InitiatePeeringEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/peers/request", async (
            [FromBody] InitiatePeeringRequest request,
            [FromServices] ISender sender,
            [FromServices] IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var serverUrl = configuration.GetValue<string>("BaseUrl") ?? "";
            var serverName = configuration.GetValue<string>("Server:Name")
                ?? (Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ? uri.Host : Environment.MachineName);

            var peerId = await sender.Send(new InitiatePeeringCommand
            {
                RemoteUrl = request.RemoteUrl,
                LocalServerName = serverName,
                LocalServerUrl = serverUrl
            }, cancellationToken);

            return Results.Created($"/api/federation/peers/{peerId}", new { Id = peerId });
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

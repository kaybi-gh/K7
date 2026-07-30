using K7.Server.Application.Features.MusicIntelligence.Commands.TestMusicIntelligenceConnection;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class TestMusicIntelligenceConnection : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/admin/music-intelligence/test", async (
            [FromServices] ISender sender,
            [FromBody] MusicIntelligenceSettingsDto? settings,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new TestMusicIntelligenceConnectionCommand { Settings = settings }, cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

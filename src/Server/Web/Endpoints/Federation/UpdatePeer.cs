using K7.Server.Application.Features.Federation.Commands.UpdatePeer;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class UpdatePeerEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/federation/peers/{id:guid}", async (
            Guid id,
            [FromBody] UpdatePeerRequest request,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpdatePeerCommand
            {
                PeerId = id,
                BaseUrl = request.BaseUrl,
                SharedLibraryIds = request.SharedLibraryIds,
                EnabledInboundAgreementIds = request.EnabledInboundAgreementIds,
                MaxConcurrentStreams = request.MaxConcurrentStreams,
                AutoAddNewLibraries = request.AutoAddNewLibraries,
                SocialAgreements = request.SocialAgreements,
                SharePlaybackHistoryLibraryIds = request.SharePlaybackHistoryLibraryIds
            }, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

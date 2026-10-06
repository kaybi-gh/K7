using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Application.Features.VideoPlayerSettings.Queries.GetEffectiveVideoPlayerSettings;
using K7.Server.Application.Services;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.LastVideoTrackSelection.Commands.UpsertLastVideoTrackSelection;

[Authorize]
public record UpsertLastVideoTrackSelectionCommand : IRequest
{
    public required Guid MediaId { get; init; }
    public required LastVideoTrackSelectionDto Selection { get; init; }
}

public class UpsertLastVideoTrackSelectionCommandHandler(
    IApplicationDbContext context,
    IUserSettingsService userSettingsService,
    ISharedProfileSettingsService sharedProfileSettingsService,
    IMediaAccessGuard accessGuard,
    IUser currentUser,
    ISender sender)
    : IRequestHandler<UpsertLastVideoTrackSelectionCommand>
{
    public async Task Handle(UpsertLastVideoTrackSelectionCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);

        var videoSettings = await sender.Send(new GetEffectiveVideoPlayerSettingsQuery(), cancellationToken);
        if (videoSettings.RememberTrackSelection == false)
            return;

        var key = await LastVideoTrackSelectionScope.ResolveSettingKeyAsync(
            context,
            request.MediaId,
            cancellationToken);
        if (key is null)
            return;

        var json = JsonSerializer.Serialize(request.Selection);
        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);

        if (sharedProfileId is { } profileId)
        {
            await SharedProfileMemberValidator.GetGroupForMemberAsync(
                context, profileId, userId, cancellationToken);
            await sharedProfileSettingsService.SetAsync(profileId, key, json, cancellationToken);
            return;
        }

        await userSettingsService.SetAsync(userId, key, json, cancellationToken);
    }
}

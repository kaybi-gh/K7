using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.SharedProfiles;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.LastVideoTrackSelection.Queries.GetLastVideoTrackSelection;

public record GetLastVideoTrackSelectionQuery : IRequest<LastVideoTrackSelectionDto?>
{
    public required Guid MediaId { get; init; }
}

public class GetLastVideoTrackSelectionQueryHandler(
    IApplicationDbContext context,
    IUserSettingsService userSettingsService,
    ISharedProfileSettingsService sharedProfileSettingsService,
    IUser currentUser)
    : IRequestHandler<GetLastVideoTrackSelectionQuery, LastVideoTrackSelectionDto?>
{
    public async Task<LastVideoTrackSelectionDto?> Handle(
        GetLastVideoTrackSelectionQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return null;

        var key = await LastVideoTrackSelectionScope.ResolveSettingKeyAsync(
            context,
            request.MediaId,
            cancellationToken);
        if (key is null)
            return null;

        string? json;
        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        if (sharedProfileId is { } profileId)
        {
            await SharedProfileMemberValidator.GetGroupForMemberAsync(
                context, profileId, userId, cancellationToken);
            json = await sharedProfileSettingsService.GetAsync(profileId, key, cancellationToken);
        }
        else
        {
            json = await userSettingsService.GetAsync(userId, key, cancellationToken);
        }

        if (json is null)
            return null;

        return JsonSerializer.Deserialize<LastVideoTrackSelectionDto>(json);
    }
}

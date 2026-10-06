using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
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
    IMediaAccessGuard accessGuard,
    IUser currentUser)
    : IRequestHandler<UpsertLastVideoTrackSelectionCommand>
{
    public async Task Handle(UpsertLastVideoTrackSelectionCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);

        var key = await LastVideoTrackSelectionScope.ResolveSettingKeyAsync(
            context,
            request.MediaId,
            cancellationToken);
        if (key is null)
            return;

        var json = JsonSerializer.Serialize(request.Selection);
        await userSettingsService.SetAsync(userId, key, json, cancellationToken);
    }
}

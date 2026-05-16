using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.VideoPlayerSettings.Commands.UpdateUserVideoPlayerSettings;

[Authorize]
public record UpdateUserVideoPlayerSettingsCommand : IRequest
{
    public required VideoPlayerSettingsDto Settings { get; init; }
}

public class UpdateUserVideoPlayerSettingsCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<UpdateUserVideoPlayerSettingsCommand>
{
    public async Task Handle(UpdateUserVideoPlayerSettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = JsonSerializer.Serialize(request.Settings);
        await userSettingsService.SetAsync(userId, UserSettingKeys.VideoPlayerSettings, json, cancellationToken);
    }
}

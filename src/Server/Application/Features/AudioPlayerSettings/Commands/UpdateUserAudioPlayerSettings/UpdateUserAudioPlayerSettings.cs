using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.AudioPlayerSettings.Commands.UpdateUserAudioPlayerSettings;

[Authorize]
public record UpdateUserAudioPlayerSettingsCommand : IRequest
{
    public required AudioPlayerSettingsDto Settings { get; init; }
}

public class UpdateUserAudioPlayerSettingsCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<UpdateUserAudioPlayerSettingsCommand>
{
    public async Task Handle(UpdateUserAudioPlayerSettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = JsonSerializer.Serialize(request.Settings);
        await userSettingsService.SetAsync(userId, UserSettingKeys.AudioPlayerSettings, json, cancellationToken);
    }
}

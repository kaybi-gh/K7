using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.TranscodeSettings.Commands.UpdateTranscodeSettings;

[Authorize(Roles = Roles.Administrator)]
public record UpdateTranscodeSettingsCommand : IRequest
{
    public required TranscodeSettingsDto Settings { get; init; }
}

public class UpdateTranscodeSettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateTranscodeSettingsCommand>
{
    public async Task Handle(UpdateTranscodeSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = request.Settings with
        {
            MaxConcurrentTranscodes = Math.Clamp(request.Settings.MaxConcurrentTranscodes, 1, 16),
            EncoderThrottleBufferSegments = Math.Clamp(request.Settings.EncoderThrottleBufferSegments, 1, 30),
            TranscodeTempQuotaMb = Math.Max(0, request.Settings.TranscodeTempQuotaMb)
        };

        await serverSettingsService.SetAsync(
            ServerSettingKeys.TranscodeSettings,
            JsonSerializer.Serialize(settings),
            cancellationToken);
    }
}

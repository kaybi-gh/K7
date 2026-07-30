using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.MusicIntelligence.Commands.TestMusicIntelligenceConnection;

[Authorize(Roles = Roles.Administrator)]
public record TestMusicIntelligenceConnectionCommand : IRequest<MusicIntelligenceConnectionResultDto>
{
    /// <summary>Optional unsaved form values so Test can run before Save.</summary>
    public MusicIntelligenceSettingsDto? Settings { get; init; }
}

public class TestMusicIntelligenceConnectionCommandHandler(IMusicIntelligenceService musicIntelligenceService)
    : IRequestHandler<TestMusicIntelligenceConnectionCommand, MusicIntelligenceConnectionResultDto>
{
    public async Task<MusicIntelligenceConnectionResultDto> Handle(TestMusicIntelligenceConnectionCommand request, CancellationToken cancellationToken)
    {
        var result = await musicIntelligenceService.TestConnectionAsync(request.Settings, cancellationToken);
        return new MusicIntelligenceConnectionResultDto
        {
            Success = result.Success,
            Version = result.Version,
            Error = result.Error
        };
    }
}

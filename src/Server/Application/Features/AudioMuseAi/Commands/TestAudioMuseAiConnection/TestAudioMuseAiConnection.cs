using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.AudioMuseAi.Commands.TestAudioMuseAiConnection;

[Authorize(Roles = Roles.Administrator)]
public record TestAudioMuseAiConnectionCommand : IRequest<AudioMuseAiConnectionResultDto>;

public class TestAudioMuseAiConnectionCommandHandler(IAudioMuseAiService audioMuseAiService)
    : IRequestHandler<TestAudioMuseAiConnectionCommand, AudioMuseAiConnectionResultDto>
{
    public async Task<AudioMuseAiConnectionResultDto> Handle(TestAudioMuseAiConnectionCommand request, CancellationToken cancellationToken)
    {
        var result = await audioMuseAiService.TestConnectionAsync(cancellationToken);
        return new AudioMuseAiConnectionResultDto
        {
            Success = result.Success,
            Version = result.Version,
            Error = result.Error
        };
    }
}

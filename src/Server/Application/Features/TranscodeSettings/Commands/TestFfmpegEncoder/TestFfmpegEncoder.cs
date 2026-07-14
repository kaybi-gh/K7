using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.TranscodeSettings.Commands.TestFfmpegEncoder;

[Authorize(Roles = Roles.Administrator)]
public record TestFfmpegEncoderCommand : IRequest<FfmpegTranscodeTestResultDto>;

public class TestFfmpegEncoderCommandHandler(IFfmpegCapabilitiesService ffmpegCapabilitiesService)
    : IRequestHandler<TestFfmpegEncoderCommand, FfmpegTranscodeTestResultDto>
{
    public Task<FfmpegTranscodeTestResultDto> Handle(TestFfmpegEncoderCommand request, CancellationToken cancellationToken) =>
        ffmpegCapabilitiesService.TestEncoderAsync(cancellationToken);
}

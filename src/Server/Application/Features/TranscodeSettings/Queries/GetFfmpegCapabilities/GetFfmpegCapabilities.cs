using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.TranscodeSettings.Queries.GetFfmpegCapabilities;

[Authorize(Roles = Roles.Administrator)]
public record GetFfmpegCapabilitiesQuery : IRequest<FfmpegCapabilitiesDto>;

public class GetFfmpegCapabilitiesQueryHandler(IFfmpegCapabilitiesService ffmpegCapabilitiesService)
    : IRequestHandler<GetFfmpegCapabilitiesQuery, FfmpegCapabilitiesDto>
{
    public Task<FfmpegCapabilitiesDto> Handle(GetFfmpegCapabilitiesQuery request, CancellationToken cancellationToken) =>
        ffmpegCapabilitiesService.GetCapabilitiesAsync(cancellationToken);
}

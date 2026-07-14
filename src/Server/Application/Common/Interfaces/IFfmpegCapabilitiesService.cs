using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Interfaces;

public interface IFfmpegCapabilitiesService
{
    Task<FfmpegCapabilitiesDto> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<FfmpegTranscodeTestResultDto> TestEncoderAsync(CancellationToken cancellationToken = default);
    Task<VideoEncoderInfoDto?> ResolveVideoEncoderAsync(string logicalCodec, bool forceSoftware = false, CancellationToken cancellationToken = default);
}

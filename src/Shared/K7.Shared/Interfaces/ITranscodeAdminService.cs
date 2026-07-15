using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface ITranscodeAdminService
{
    Task<TranscodeSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(TranscodeSettingsDto settings, CancellationToken cancellationToken = default);
    Task<FfmpegCapabilitiesDto> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<FfmpegTranscodeTestResultDto> TestEncoderAsync(CancellationToken cancellationToken = default);
}

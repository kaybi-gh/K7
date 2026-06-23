using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface IAudioMuseAiAdminService
{
    Task<AudioMuseAiSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(AudioMuseAiSettingsDto settings, CancellationToken cancellationToken = default);
    Task<AudioMuseAiConnectionResultDto> TestConnectionAsync(CancellationToken cancellationToken = default);
}

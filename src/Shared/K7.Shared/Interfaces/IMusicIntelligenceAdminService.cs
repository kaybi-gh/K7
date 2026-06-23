using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface IMusicIntelligenceAdminService
{
    Task<MusicIntelligenceSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(MusicIntelligenceSettingsDto settings, CancellationToken cancellationToken = default);
    Task<MusicIntelligenceConnectionResultDto> TestConnectionAsync(CancellationToken cancellationToken = default);
}

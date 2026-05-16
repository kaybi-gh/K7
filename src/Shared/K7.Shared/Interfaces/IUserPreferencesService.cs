using K7.Shared.Dtos;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;

namespace K7.Shared.Interfaces;

public interface IUserPreferencesService
{
    Task<IReadOnlyList<Guid>> GetSelfLibraryExclusionsAsync(CancellationToken cancellationToken = default);
    Task UpdateSelfLibraryExclusionsAsync(UpdateSelfLibraryExclusionsRequest request, CancellationToken cancellationToken = default);
    Task<HomeLayoutDto> GetHomeLayoutAsync(CancellationToken cancellationToken = default);
    Task UpdateHomeLayoutAsync(HomeLayoutDto layout, CancellationToken cancellationToken = default);
    Task ResetHomeLayoutAsync(CancellationToken cancellationToken = default);
    Task<VideoPlayerSettingsDto> GetEffectiveVideoPlayerSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateUserVideoPlayerSettingsAsync(VideoPlayerSettingsDto settings, CancellationToken cancellationToken = default);
    Task ResetUserVideoPlayerSettingsAsync(CancellationToken cancellationToken = default);
}

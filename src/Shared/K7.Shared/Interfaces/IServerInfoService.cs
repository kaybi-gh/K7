using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Shared.Interfaces;

public interface IServerInfoService
{
    Task<AboutInfoDto?> GetAboutInfoAsync(CancellationToken cancellationToken = default);
    Task<ServerInfoDto?> GetServerInfoAsync(CancellationToken cancellationToken = default);
    Task<AuthenticationInfoDto?> GetAuthenticationInfoAsync(CancellationToken cancellationToken = default);
    Task<WatchStatsDto?> GetWatchStatsAsync(string? mediaType = null, string period = "month", DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<PlaybackHistoryPageDto?> GetPlaybackHistoryAsync(int page = 1, int pageSize = 25, string? mediaType = null, CancellationToken cancellationToken = default);
    Task<List<MediaDto>?> GetMusicRadioAsync(string radioType, Guid[]? libraryIds = null, Guid[]? libraryGroupIds = null, Guid? seedTrackId = null, Guid? seedArtistId = null, string? moodPreset = null, int limit = 50, CancellationToken cancellationToken = default);
    Task UpdateDefaultLanguageAsync(string language, CancellationToken cancellationToken = default);
    Task UpdateDefaultThemeAsync(string theme, CancellationToken cancellationToken = default);
    Task<List<ActiveStreamDto>?> GetActiveStreamsAsync(CancellationToken cancellationToken = default);
    Task<ServerMetricsHistoryDto?> GetServerMetricsAsync(CancellationToken cancellationToken = default);
    Task<PlaybackHistoryPageDto?> GetAdminPlaybackHistoryAsync(int page = 1, int pageSize = 25, string? mediaType = null, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<WatchStatsDto?> GetAdminWatchStatsAsync(string? mediaType = null, string period = "month", Guid? userId = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
}

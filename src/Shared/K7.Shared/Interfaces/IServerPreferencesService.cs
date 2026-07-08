using K7.Shared.Dtos;
using K7.Shared.Dtos.Home;

namespace K7.Shared.Interfaces;

public interface IServerPreferencesService
{
    Task<HomeLayoutDto?> GetServerHomeLayoutAsync(CancellationToken cancellationToken = default);
    Task<HomeLayoutDto> GetEffectiveServerHomeLayoutAsync(CancellationToken cancellationToken = default);
    Task UpdateServerHomeLayoutAsync(HomeLayoutDto layout, CancellationToken cancellationToken = default);
    Task DeleteServerHomeLayoutAsync(CancellationToken cancellationToken = default);
    Task<ServerFeatureFlagsDto> GetServerFeatureFlagsAsync(CancellationToken cancellationToken = default);
    Task UpdateServerFeatureFlagsAsync(ServerFeatureFlagsDto flags, CancellationToken cancellationToken = default);
    Task<MusicIntelligenceStatusDto> GetMusicIntelligenceStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MusicMoodPresetDto>> GetMusicMoodPresetsAsync(CancellationToken cancellationToken = default);
    Task<VideoPlayerSettingsDto?> GetServerVideoPlayerSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateServerVideoPlayerSettingsAsync(VideoPlayerSettingsDto settings, CancellationToken cancellationToken = default);
    Task DeleteServerVideoPlayerSettingsAsync(CancellationToken cancellationToken = default);
    Task<AudioPlayerSettingsDto?> GetServerAudioPlayerSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateServerAudioPlayerSettingsAsync(AudioPlayerSettingsDto settings, CancellationToken cancellationToken = default);
    Task DeleteServerAudioPlayerSettingsAsync(CancellationToken cancellationToken = default);
    Task<TrackSelectionPreferencesDto?> GetServerTrackSelectionPreferencesAsync(Guid? libraryId = null, CancellationToken cancellationToken = default);
    Task UpdateServerTrackSelectionPreferencesAsync(TrackSelectionPreferencesDto preferences, Guid? libraryId = null, CancellationToken cancellationToken = default);
    Task DeleteServerTrackSelectionPreferencesAsync(Guid? libraryId = null, CancellationToken cancellationToken = default);
    Task<VideoPlaybackPolicySettingsDto?> GetServerVideoPlaybackPolicySettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateServerVideoPlaybackPolicySettingsAsync(VideoPlaybackPolicySettingsDto settings, CancellationToken cancellationToken = default);
    Task DeleteServerVideoPlaybackPolicySettingsAsync(CancellationToken cancellationToken = default);
    Task<AudioPlaybackPolicySettingsDto?> GetServerAudioPlaybackPolicySettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateServerAudioPlaybackPolicySettingsAsync(AudioPlaybackPolicySettingsDto settings, CancellationToken cancellationToken = default);
    Task DeleteServerAudioPlaybackPolicySettingsAsync(CancellationToken cancellationToken = default);
}

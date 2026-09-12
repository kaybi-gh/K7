using K7.Shared.Dtos.Scrobbling;

namespace K7.Shared.Interfaces;

public interface IScrobblingAdminService
{
    Task<ScrobblingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(ScrobblingSettingsDto settings, CancellationToken cancellationToken = default);
}

public interface IScrobblingUserService
{
    Task<ScrobblingAvailabilityDto> GetAvailabilityAsync(CancellationToken cancellationToken = default);
    Task<List<UserScrobblerAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAccountAsync(CreateUserScrobblerAccountRequest request, CancellationToken cancellationToken = default);
    Task UpdateAccountAsync(Guid id, UpdateUserScrobblerAccountRequest request, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ScrobbleTestResultDto> TestAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<ScrobbleWebhookPresetDto>> GetWebhookPresetsAsync(CancellationToken cancellationToken = default);
    Task<LastFmAuthStartDto> StartLastFmAuthAsync(CancellationToken cancellationToken = default);
    Task<Guid> CompleteLastFmAuthAsync(
        string token,
        IReadOnlyList<string>? mediaTypes = null,
        CancellationToken cancellationToken = default);
    Task<TraktDeviceStartDto> StartTraktDeviceAsync(CancellationToken cancellationToken = default);
    Task<Guid?> PollTraktDeviceAsync(
        string deviceCode,
        IReadOnlyList<string>? mediaTypes = null,
        string? displayName = null,
        CancellationToken cancellationToken = default);
}

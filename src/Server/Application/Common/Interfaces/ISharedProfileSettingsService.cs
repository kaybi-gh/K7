using K7.Server.Domain.Settings;

namespace K7.Server.Application.Common.Interfaces;

public interface ISharedProfileSettingsService
{
    Task<T?> GetAsync<T>(Guid sharedProfileId, SettingKey<T> key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(Guid sharedProfileId, SettingKey<T> key, T value, CancellationToken cancellationToken = default);
    Task RemoveAsync<T>(Guid sharedProfileId, SettingKey<T> key, CancellationToken cancellationToken = default);
}

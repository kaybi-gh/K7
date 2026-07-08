using K7.Server.Domain.Settings;

namespace K7.Server.Application.Common.Interfaces;

public interface IUserSettingsService
{
    Task<T?> GetAsync<T>(Guid userId, SettingKey<T> key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(Guid userId, SettingKey<T> key, T value, CancellationToken cancellationToken = default);
    Task RemoveAsync<T>(Guid userId, SettingKey<T> key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid userId, string key, CancellationToken cancellationToken = default);
}

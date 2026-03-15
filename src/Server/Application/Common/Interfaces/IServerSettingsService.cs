using K7.Server.Domain.Settings;

namespace K7.Server.Application.Common.Interfaces;

public interface IServerSettingsService
{
    Task<T?> GetAsync<T>(SettingKey<T> key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(SettingKey<T> key, T value, CancellationToken cancellationToken = default);
}

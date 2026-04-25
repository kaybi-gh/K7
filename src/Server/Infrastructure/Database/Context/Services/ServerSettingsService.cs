using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Settings;
using K7.Server.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Infrastructure.Database.Context.Services;

public class ServerSettingsService(IApplicationDbContext context) : IServerSettingsService
{
    public async Task<T?> GetAsync<T>(SettingKey<T> key, CancellationToken cancellationToken = default)
    {
        var setting = await context.ServerSettings
            .FirstOrDefaultAsync(s => s.Key == key.Name, cancellationToken);

        if (setting is null)
            return key.DefaultValue;

        return JsonSerializer.Deserialize<T>(setting.Value);
    }

    public async Task SetAsync<T>(SettingKey<T> key, T value, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value);
        await SetRawAsync(key.Name, serialized, cancellationToken);
    }

    public async Task<object?> GetAsync(ISettingKey key, CancellationToken cancellationToken = default)
    {
        var setting = await context.ServerSettings
            .FirstOrDefaultAsync(s => s.Key == key.Name, cancellationToken);

        if (setting is null)
            return key.BoxedDefaultValue;

        return JsonSerializer.Deserialize(setting.Value, key.ValueType);
    }

    public async Task SetAsync(ISettingKey key, object value, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value, key.ValueType);
        await SetRawAsync(key.Name, serialized, cancellationToken);
    }

    public async Task RemoveAsync<T>(SettingKey<T> key, CancellationToken cancellationToken = default)
    {
        var setting = await context.ServerSettings
            .FirstOrDefaultAsync(s => s.Key == key.Name, cancellationToken);

        if (setting is not null)
        {
            context.ServerSettings.Remove(setting);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SetRawAsync(string keyName, string serialized, CancellationToken cancellationToken)
    {
        var setting = await context.ServerSettings
            .FirstOrDefaultAsync(s => s.Key == keyName, cancellationToken);

        if (setting is null)
        {
            context.ServerSettings.Add(new ServerSetting { Key = keyName, Value = serialized });
        }
        else
        {
            setting.Value = serialized;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

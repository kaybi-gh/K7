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
            return default;

        return JsonSerializer.Deserialize<T>(setting.Value);
    }

    public async Task SetAsync<T>(SettingKey<T> key, T value, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value);
        var setting = await context.ServerSettings
            .FirstOrDefaultAsync(s => s.Key == key.Name, cancellationToken);

        if (setting is null)
        {
            context.ServerSettings.Add(new ServerSetting { Key = key.Name, Value = serialized });
        }
        else
        {
            setting.Value = serialized;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

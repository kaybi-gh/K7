using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Settings;
using K7.Server.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Infrastructure.Database.Context.Services;

public class UserSettingsService(IApplicationDbContext context) : IUserSettingsService
{
    public async Task<T?> GetAsync<T>(Guid userId, SettingKey<T> key, CancellationToken cancellationToken = default)
    {
        var setting = await context.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key.Name, cancellationToken);

        if (setting is null)
            return key.DefaultValue;

        return JsonSerializer.Deserialize<T>(setting.Value);
    }

    public async Task SetAsync<T>(Guid userId, SettingKey<T> key, T value, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value);

        var setting = await context.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key.Name, cancellationToken);

        if (setting is null)
        {
            context.UserSettings.Add(new UserSetting { UserId = userId, Key = key.Name, Value = serialized });
        }
        else
        {
            setting.Value = serialized;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync<T>(Guid userId, SettingKey<T> key, CancellationToken cancellationToken = default)
    {
        var setting = await context.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key.Name, cancellationToken);

        if (setting is not null)
        {
            context.UserSettings.Remove(setting);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<bool> ExistsAsync(Guid userId, string key, CancellationToken cancellationToken = default) =>
        context.UserSettings.AnyAsync(s => s.UserId == userId && s.Key == key, cancellationToken);
}

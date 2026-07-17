using System.Text.Json;
using K7.Server.Application.Common.Extensions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Settings;
using K7.Server.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace K7.Server.Infrastructure.Database.Context.Services;

public class SharedProfileSettingsService(IApplicationDbContext context, IMemoryCache cache) : ISharedProfileSettingsService
{
    private static string CacheKey(Guid sharedProfileId, string keyName) =>
        $"shared-profile-setting:{sharedProfileId}:{keyName}";

    public async Task<T?> GetAsync<T>(Guid sharedProfileId, SettingKey<T> key, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(sharedProfileId, key.Name);
        if (cache.TryGetValue(cacheKey, out T? cached))
            return cached;

        var setting = await context.SharedProfileSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SharedProfileId == sharedProfileId && s.Key == key.Name, cancellationToken);

        var value = setting is null
            ? key.DefaultValue
            : JsonSerializer.Deserialize<T>(setting.Value);

        cache.SetWithSize(cacheKey, value);
        return value;
    }

    public async Task SetAsync<T>(Guid sharedProfileId, SettingKey<T> key, T value, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value);

        var setting = await context.SharedProfileSettings
            .FirstOrDefaultAsync(s => s.SharedProfileId == sharedProfileId && s.Key == key.Name, cancellationToken);

        if (setting is null)
        {
            context.SharedProfileSettings.Add(new SharedProfileSetting
            {
                SharedProfileId = sharedProfileId,
                Key = key.Name,
                Value = serialized
            });
        }
        else
        {
            setting.Value = serialized;
        }

        await context.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey(sharedProfileId, key.Name));
    }

    public async Task RemoveAsync<T>(Guid sharedProfileId, SettingKey<T> key, CancellationToken cancellationToken = default)
    {
        var setting = await context.SharedProfileSettings
            .FirstOrDefaultAsync(s => s.SharedProfileId == sharedProfileId && s.Key == key.Name, cancellationToken);

        if (setting is not null)
        {
            context.SharedProfileSettings.Remove(setting);
            await context.SaveChangesAsync(cancellationToken);
            cache.Remove(CacheKey(sharedProfileId, key.Name));
        }
    }
}

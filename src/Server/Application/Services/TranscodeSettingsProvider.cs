using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.Services;

public class TranscodeSettingsProvider(IServiceScopeFactory scopeFactory) : ITranscodeSettingsProvider
{
    public async Task<TranscodeSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var serverSettingsService = scope.ServiceProvider.GetRequiredService<IServerSettingsService>();
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.TranscodeSettings, cancellationToken);

        if (string.IsNullOrEmpty(json))
            return new TranscodeSettingsDto();

        return JsonSerializer.Deserialize<TranscodeSettingsDto>(json) ?? new TranscodeSettingsDto();
    }
}

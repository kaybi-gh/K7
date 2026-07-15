using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Interfaces;

public interface ITranscodeSettingsProvider
{
    Task<TranscodeSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
}

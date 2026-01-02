using K7.Server.Domain.Entities.MediaFormats;

namespace K7.Server.Domain.Interfaces;

public interface IMediaFormatSampleGenerator
{
    Task<MemoryStream> GenerateSampleAsync(BaseMediaFormat mediaFormat);
}

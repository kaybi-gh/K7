using K7.Server.Domain.Models;

namespace K7.Server.Domain.Interfaces;

public interface IAudioTagReader
{
    AudioTagData? ReadTags(string filePath, bool includeCoverArt = true);
}

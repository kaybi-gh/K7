using K7.Server.Domain.ValueObjects;

namespace K7.Server.Domain.Interfaces;

public interface IAudioTagReader
{
    AudioTagData? ReadTags(string filePath);
}

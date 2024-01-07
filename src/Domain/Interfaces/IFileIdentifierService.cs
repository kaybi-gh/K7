using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Interfaces;
public interface IFileIdentifierService
{
    public BaseMedia Identify(string filePath);
}

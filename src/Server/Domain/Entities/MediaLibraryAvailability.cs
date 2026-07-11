using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Entities;

public class MediaLibraryAvailability
{
    public Guid LibraryId { get; set; }
    public Library? Library { get; set; }

    public Guid MediaId { get; set; }
    public BaseMedia? Media { get; set; }
}

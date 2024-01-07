using MediaServer.Domain.Entities.Files;
using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Entities;

public class Library : BaseAuditableEntity
{
    public required string Title { get; set; }
    public required LibraryMediaType MediaType { get; set; }
    public required string RootPath { get; set; }
    public bool? RootPathAccessible { get; set; }

    public IList<Files.MediaFile> Files { get; set; } = new List<Files.MediaFile>();
    public IList<BaseMedia> Items { get; private set; } = new List<BaseMedia>();
}

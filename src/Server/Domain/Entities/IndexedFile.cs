using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Models;

namespace K7.Server.Domain.Entities;
public class IndexedFile : BaseAuditableEntity
{
    public required Guid LibraryId { get; set; }
    public required string Name { get; set; }
    public required string Extension { get; set; }
    public required string Path { get; set; }
    public string? ParentDirectory { get; set; }
    public required uint Hash { get; set; }
    public required long Size { get; set; }
    public DateTimeOffset LastWriteTimeUtc { get; set; }

    public MediaIdentification? Identification { get; set; }
    public BaseFileMetadata? FileMetadata { get; set; }
    public Guid? MediaId { get; set; }
    public BaseMedia? Media { get; set; }

    public byte[]? ChromaprintFingerprint { get; set; }
    public int? ChromaprintDurationSeconds { get; set; }
    public DateTimeOffset? ChromaprintAnalyzedAt { get; set; }
}

// Episode:
// One file can represent one episode or a part of an episode or multiple episodes
// Can have one or two parent directories or none
// Serie title must be in filename or parent directory or parent parent directory
// Episode(s) number(s) must be in filename
// Season number must exist and can be in filename or parent directory
// Episode(s) title(s) can be in filename
// Release year should be in filename or parent directory or parent parent directory

// Movie:
// One file can represent one movie or a part of a movie
// Can have a parent directory or none
// Title must be in filename or parent directory
// Release year should be in filename or parent directory

// Track
// One file must represent one track
// Can have one or two parent directories or none
// Artist name must be in filename or parent directory or parent parent directory
// Album name must be in filename or parent directory
// Track title can be in filename
// Album release year must be in filename or parent directory

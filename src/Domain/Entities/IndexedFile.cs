namespace MediaServer.Domain.Entities;
public class IndexedFile : BaseAuditableEntity
{
    public required int LibraryId { get; set; }
    public required string Name { get; set; }
    public required string Extension { get; set; }
    public required string Path { get; set; }
    public string? ParentDirectory { get; set; }
    public required string Hash { get; set; }
    public required long Size { get; set; }
    public bool IsSplitPart { get; set; } = false;
    public bool IsComposite { get; set; } = false;
    public bool IsIdentified { get; set; } = false;
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

namespace K7.Server.Domain.Entities.Metadatas.Files;

public sealed class ChapterMarker
{
    public double StartSeconds { get; set; }
    public double? EndSeconds { get; set; }
    public string? Title { get; set; }
}

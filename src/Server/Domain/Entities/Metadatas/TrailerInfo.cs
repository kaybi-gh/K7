namespace K7.Server.Domain.Entities.Metadatas;

public class TrailerInfo
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public required string Site { get; set; }
    public required string Type { get; set; }
    public string? Language { get; set; }
}

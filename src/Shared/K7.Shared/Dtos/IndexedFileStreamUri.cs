namespace K7.Shared.Dtos;
public sealed record IndexedFileStreamUri
{
    public required Uri Uri { get; set; }
    public required string MimeType { get; set; }
}

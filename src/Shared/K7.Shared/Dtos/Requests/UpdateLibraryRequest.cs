namespace K7.Shared.Dtos.Requests;

public sealed record UpdateLibraryRequest
{
    public string? Title { get; init; }
    public string? MetadataProviderName { get; init; }
    public int? MetadataRefreshIntervalDays { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
}

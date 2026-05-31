namespace K7.Shared.Dtos.Requests;

public sealed record UpdateLibraryRequest
{
    public string? Title { get; init; }
    public string? MetadataProviderName { get; init; }
    public string? MetadataLanguage { get; init; }
    public string? MetadataFallbackLanguage { get; init; }
    public int? MetadataRefreshIntervalDays { get; init; }
    public Guid? LibraryGroupId { get; init; }
}

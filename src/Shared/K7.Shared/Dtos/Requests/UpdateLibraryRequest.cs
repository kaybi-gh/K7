namespace K7.Shared.Dtos.Requests;

public sealed record UpdateLibraryRequest
{
    public required Guid Id { get; init; }
    public string? Title { get; init; }
    public string? MetadataProviderName { get; init; }
}

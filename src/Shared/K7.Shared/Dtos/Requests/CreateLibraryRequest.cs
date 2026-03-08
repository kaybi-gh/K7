using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record CreateLibraryRequest
{
    public required string Title { get; init; }
    public required LibraryMediaType MediaType { get; init; }
    public required string RootPath { get; init; }
    public bool TriggerFileIndexingOnCreation { get; init; } = true;
}

using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record LibraryDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required LibraryMediaType MediaType { get; init; }
    public required string RootPath { get; init; }

    public static LibraryDto FromDomain(Library domain) => new()
    {
        Id = domain.Id,
        Title = domain.Title,
        MediaType = domain.MediaType,
        RootPath = domain.RootPath
    };
}

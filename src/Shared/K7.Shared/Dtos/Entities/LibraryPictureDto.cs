using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record LibraryPictureDto
{
    public required Guid Id { get; init; }
    public required MetadataPictureType Type { get; init; }
    public string? DominantColor { get; init; }
}

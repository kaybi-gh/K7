using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record PeerShareAgreementDto
{
    public required Guid Id { get; init; }
    public required Guid LibraryId { get; init; }
    public string? LibraryTitle { get; init; }
    public required ShareDirection Direction { get; init; }
    public int? MaxConcurrentStreams { get; init; }
    public bool IsEnabled { get; init; }
}

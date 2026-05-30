using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record PeerServerDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
    public required PeerStatus Status { get; init; }
    public DateTimeOffset? LastSeen { get; init; }
    public DateTimeOffset Created { get; init; }
    public IReadOnlyList<PeerShareAgreementDto> ShareAgreements { get; init; } = [];
}

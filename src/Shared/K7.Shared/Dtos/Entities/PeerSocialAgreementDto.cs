using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record PeerSocialAgreementDto
{
    public required Guid Id { get; init; }
    public required FederationContentType ContentType { get; init; }
    public bool AllowOutbound { get; init; }
    public bool AllowInbound { get; init; }
}

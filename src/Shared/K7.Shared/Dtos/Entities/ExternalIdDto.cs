using K7.Server.Domain.Entities;

namespace K7.Shared.Dtos.Entities;

public sealed record ExternalIdDto
{
    public Guid Id { get; init; }
    public required string Platform { get; init; }
    public required string Value { get; init; }

    public static ExternalIdDto FromDomain(ExternalId domain) => new()
    {
        Id = domain.Id,
        Platform = domain.Platform,
        Value = domain.Platform
    };
}

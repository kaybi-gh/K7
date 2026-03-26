using K7.Shared.Dtos.Users;

namespace K7.Shared.Dtos.Requests;

public sealed record UpdateUserCapabilitiesRequest
{
    public required IReadOnlyList<CapabilityOverrideDto> Overrides { get; init; }
}

using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Diagnostics;

public sealed record FixDiagnosticItemsRequest
{
    public required IReadOnlyList<Guid> EntityIds { get; init; }
    public required DiagnosticFixAction Action { get; init; }
}

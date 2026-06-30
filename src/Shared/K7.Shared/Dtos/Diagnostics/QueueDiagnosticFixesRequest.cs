using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Diagnostics;

public sealed record QueueDiagnosticFixesRequest
{
    public required DiagnosticIssue Issue { get; init; }
    public Guid? LibraryId { get; init; }
}

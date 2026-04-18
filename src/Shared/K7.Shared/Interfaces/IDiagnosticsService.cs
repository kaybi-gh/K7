using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Diagnostics;

namespace K7.Shared.Interfaces;

public interface IDiagnosticsService
{
    Task<List<LibraryHealthSummaryDto>> GetDiagnosticsSummaryAsync(CancellationToken cancellationToken = default);
    Task<PaginatedListDto<DiagnosticItemDto>> GetDiagnosticItemsAsync(Guid? libraryId = null, DiagnosticEntityType? entityType = null, DiagnosticIssue? issue = null, IReadOnlyCollection<DiagnosticIssue>? issues = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<int> FixDiagnosticItemsAsync(IReadOnlyList<Guid> entityIds, DiagnosticFixAction action, CancellationToken cancellationToken = default);
}

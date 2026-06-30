using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;

namespace K7.Shared.Interfaces;

public interface IBackgroundTaskService
{
    Task<PaginatedListDto<BackgroundTaskDto>> GetBackgroundTasksAsync(int pageNumber = 1, int pageSize = 20, IReadOnlyCollection<BackgroundTaskStatus>? statuses = null, IReadOnlyCollection<string>? names = null, string? sortBy = null, bool sortDescending = true, CancellationToken cancellationToken = default);
    Task<BackgroundTaskDto> GetBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BackgroundTaskSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(UpdateBackgroundTaskSettingsRequest request, CancellationToken cancellationToken = default);
    Task<BackgroundTaskSummaryDto> GetSummaryAsync(
        IReadOnlyCollection<BackgroundTaskStatus>? statusFilter = null,
        IReadOnlyCollection<string>? namesFilter = null,
        CancellationToken cancellationToken = default);
}

using K7.Clients.Shared.Enums;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Diagnostics;

namespace K7.Clients.Shared.Models;

public sealed record BackgroundTasksFilterState(
    BackgroundTaskStatus? Status,
    string? TaskType,
    BackgroundTaskOrderingOption Sort = BackgroundTaskOrderingOption.None,
    string? SortBy = null,
    bool SortDescending = true);

public sealed record DiagnosticsFilterState(
    string? Severity,
    Guid? LibraryId,
    DiagnosticEntityType? EntityType,
    DiagnosticIssue? Issue);

public sealed record AdminPlaybackHistoryFilterState(
    Guid? UserId,
    string MediaType);

public sealed record UserPlaybackHistoryFilterState(string MediaType, string Period, string? From = null, string? To = null);

public sealed record UserWatchStatsFilterState(string MediaType, string Period, string? From = null, string? To = null);

public sealed record AdminWatchStatsFilterState(
    Guid? UserId,
    string MediaType,
    string Period,
    string? From = null,
    string? To = null);

public sealed record LibraryGroupFilterState(
    int MediaType,
    int Sort,
    string? FilterJson,
    string? IntelligentSearchJson);

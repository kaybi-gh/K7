using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminBackgroundTasksPanel : IDisposable
{
    private const string FilterStorageKey = "admin.background-tasks";
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);
    private static readonly BackgroundTaskStatus[] _allStatuses =
    [
        BackgroundTaskStatus.Pending,
        BackgroundTaskStatus.InProgress,
        BackgroundTaskStatus.WaitingForRetry,
        BackgroundTaskStatus.Completed,
        BackgroundTaskStatus.Failed
    ];

    [Inject] private IBackgroundTaskService BackgroundTaskService { get; set; } = default!;
    [Inject] private IFederationService FederationService { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private K7.Clients.Shared.Services.K7HubClient K7HubClient { get; set; } = default!;
    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "status")]
    public string? QueryStatus { get; set; }

    [SupplyParameterFromQuery(Name = "taskType")]
    public string? QueryTaskType { get; set; }

    [SupplyParameterFromQuery(Name = "sort")]
    public string? QuerySort { get; set; }

    private BackgroundTaskSettingsDto? _settings;
    private BackgroundTaskSummaryDto? _summary;
    private K7DataTable<BackgroundTaskDto>? _tableRef;
    private bool _isSavingSettings;
    private BackgroundTaskStatus? _selectedStatus;
    private string? _selectedTaskType;
    private BackgroundTaskOrderingOption _selectedSort = BackgroundTaskOrderingOption.DateDesc;
    private int _totalCount;
    private int _workerCount;
    private Dictionary<string, int> _concurrencyLimits = new();
    private Dictionary<Guid, string> _peerNames = new();
    private readonly CancellationTokenSource _cts = new();
    private Timer? _debounceTimer;
    private const int PageSize = 50;
    private int _tableKey;
    private bool _pendingQuerySync;

    private static readonly List<BackgroundTaskOrderingOption> SortOptions =
    [
        BackgroundTaskOrderingOption.DateDesc,
        BackgroundTaskOrderingOption.DateAsc,
        BackgroundTaskOrderingOption.DurationDesc,
        BackgroundTaskOrderingOption.DurationAsc,
        BackgroundTaskOrderingOption.NameAsc,
        BackgroundTaskOrderingOption.NameDesc
    ];

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.BackgroundTaskUpdated += OnBackgroundTaskUpdated;

        if (PageFilterUrlSync.HasAnyQuery(Navigation, "status", "taskType", "sort"))
        {
            ApplyFiltersFromQuery();
            await SaveFiltersToStorageAsync();
            _tableKey++;
        }
        else if (await LoadPersistedFiltersAsync())
        {
            _tableKey++;
            _pendingQuerySync = true;
        }

        await Task.WhenAll(LoadSettingsAsync(initial: true), LoadSummaryAsync(), LoadPeerNamesAsync());
    }

    protected override void OnAfterRender(bool firstRender) =>
        PageFilterUrlSync.SyncAfterRender(Navigation, firstRender, ref _pendingQuerySync, BuildFilterQuery());

    private void ApplyFiltersFromQuery()
    {
        var statusValue = QueryStatus ?? PageFilterUrlSync.GetQueryValue(Navigation, "status");
        _selectedStatus = Enum.TryParse<BackgroundTaskStatus>(statusValue, ignoreCase: true, out var status) ? status : null;
        _selectedTaskType = QueryTaskType ?? PageFilterUrlSync.GetQueryValue(Navigation, "taskType");

        var sortValue = QuerySort ?? PageFilterUrlSync.GetQueryValue(Navigation, "sort");
        if (Enum.TryParse<BackgroundTaskOrderingOption>(sortValue, ignoreCase: true, out var sort)
            && sort is not BackgroundTaskOrderingOption.None)
        {
            _selectedSort = sort;
        }
    }

    private void SyncFiltersToQuery() =>
        PageFilterUrlSync.SetQuery(Navigation, BuildFilterQuery());

    private Dictionary<string, string?> BuildFilterQuery() => new()
    {
        ["status"] = _selectedStatus?.ToString(),
        ["taskType"] = _selectedTaskType,
        ["sort"] = _selectedSort is BackgroundTaskOrderingOption.DateDesc ? null : _selectedSort.ToString()
    };

    private async Task<bool> LoadPersistedFiltersAsync()
    {
        try
        {
            var state = await PageFilterStorage.LoadAsync<BackgroundTasksFilterState>(FilterStorageKey, CancellationToken.None);
            if (state is null)
            {
                return false;
            }

            _selectedStatus = state.Status;
            _selectedTaskType = state.TaskType;
            _selectedSort = state.Sort is not BackgroundTaskOrderingOption.None
                ? state.Sort
                : MapLegacySort(state.SortBy, state.SortDescending);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SaveFiltersToStorageAsync()
    {
        try
        {
            await PageFilterStorage.SaveAsync(
                FilterStorageKey,
                new BackgroundTasksFilterState(_selectedStatus, _selectedTaskType, _selectedSort),
                CancellationToken.None);
        }
        catch
        {
            // Non-critical
        }
    }

    private async Task PersistFiltersAsync()
    {
        await SaveFiltersToStorageAsync();
        SyncFiltersToQuery();
    }

    private bool HasActiveFilters =>
        _selectedStatus.HasValue
        || _selectedTaskType is not null;

    private string? GetStatusFilterSummary() =>
        _selectedStatus is { } status ? GetStatusLabel(status) : null;

    private string? GetTaskTypeFilterSummary() =>
        _selectedTaskType is not null ? GetTaskTypeLabel(_selectedTaskType) : null;

    private string GetTaskTypeMenuLabel()
    {
        var summary = GetTaskTypeFilterSummary();
        return summary is null ? L["FilterTaskType"] : $"{L["FilterTaskType"]}: {summary}";
    }

    private async Task ClearFiltersAsync()
    {
        _selectedStatus = null;
        _selectedTaskType = null;
        await PageFilterStorage.ClearAsync(FilterStorageKey, CancellationToken.None);
        SyncFiltersToQuery();
        await Task.WhenAll(LoadSummaryAsync(), RefreshTableAsync());
    }

    private string GetActiveFiltersLabel()
    {
        var parts = new List<string>();
        if (_selectedStatus is { } status)
        {
            parts.Add(GetStatusLabel(status));
        }

        if (_selectedTaskType is not null)
        {
            parts.Add(GetTaskTypeLabel(_selectedTaskType));
        }

        return string.Join(" · ", parts);
    }

    public void Dispose()
    {
        K7HubClient.BackgroundTaskUpdated -= OnBackgroundTaskUpdated;
        _debounceTimer?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }

    private void OnBackgroundTaskUpdated()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            _ = InvokeAsync(async () =>
            {
                await Task.WhenAll(LoadSummaryAsync(), LoadSettingsAsync());
                StateHasChanged();
            });
        }, null, DebounceDelay, Timeout.InfiniteTimeSpan);
    }

    private void OnColumnPickerClick() => _tableRef?.ToggleColumnPicker();

    private async Task RefreshTableAsync()
    {
        _tableKey++;
        await InvokeAsync(StateHasChanged);
    }

    private async Task<K7DataTableResult<BackgroundTaskDto>> LoadServerDataAsync(
        K7DataTableState<BackgroundTaskDto> state, CancellationToken cancellationToken)
    {
        var startIndex = state.StartIndex;
        var count = state.Count;
        if (count <= 0) return new K7DataTableResult<BackgroundTaskDto>([], 0);

        var statuses = _selectedStatus.HasValue ? new[] { _selectedStatus.Value } : null;
        var names = _selectedTaskType is not null ? new[] { _selectedTaskType } : null;

        var firstPage = (startIndex / PageSize) + 1;
        var lastPage = ((startIndex + count - 1) / PageSize) + 1;
        var (sortBy, sortDescending) = MapSortToApi(_selectedSort);

        try
        {
            var tasks = Enumerable.Range(firstPage, lastPage - firstPage + 1)
                .Select(page => BackgroundTaskService.GetBackgroundTasksAsync(page, PageSize, statuses, names, sortBy, sortDescending, cancellationToken));

            var results = await Task.WhenAll(tasks);

            var totalCount = 0;
            var allItems = new List<BackgroundTaskDto>(count);
            foreach (var result in results)
            {
                if (result is null)
                {
                    continue;
                }

                if (result.TotalCount is { } tc)
                {
                    totalCount = tc;
                }

                if (result.Items is { Count: > 0 })
                {
                    allItems.AddRange(result.Items);
                }
            }

            var offset = startIndex - (firstPage - 1) * PageSize;
            var items = allItems.Skip(offset).Take(count).ToList();
            _totalCount = totalCount;
            await InvokeAsync(StateHasChanged);

            return new K7DataTableResult<BackgroundTaskDto>(items, totalCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Snackbar.Add(S["LoadError"], K7Severity.Error);
            return new K7DataTableResult<BackgroundTaskDto>([], 0);
        }
    }

    private async Task LoadSettingsAsync(bool initial = false)
    {
        try
        {
            _settings = await BackgroundTaskService.GetSettingsAsync(_cts.Token);
            if (initial)
            {
                _workerCount = _settings.WorkerCount;
                _concurrencyLimits = _settings.ConcurrencyGroups.ToDictionary(g => g.Name, g => g.Limit);
            }
        }
        catch (OperationCanceledException)
        {
            // Component disposed
        }
        catch
        {
            _settings = null;
        }
    }

    private async Task LoadSummaryAsync()
    {
        try
        {
            IReadOnlyCollection<BackgroundTaskStatus>? statusFilter = _selectedStatus.HasValue
                ? [_selectedStatus.Value]
                : null;
            IReadOnlyCollection<string>? namesFilter = _selectedTaskType is not null
                ? [_selectedTaskType]
                : null;

            _summary = await BackgroundTaskService.GetSummaryAsync(statusFilter, namesFilter, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Component disposed
        }
        catch
        {
            _summary = null;
        }
    }

    private async Task SaveSettingsAsync()
    {
        _isSavingSettings = true;
        try
        {
            var request = new UpdateBackgroundTaskSettingsRequest
            {
                WorkerCount = _workerCount,
                ConcurrencyLimits = _concurrencyLimits
            };
            await BackgroundTaskService.UpdateSettingsAsync(request, _cts.Token);
            Snackbar.Add(L["SettingsSaved"], K7Severity.Success);
            await LoadSettingsAsync(initial: true);
        }
        catch (OperationCanceledException)
        {
            // Component disposed
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isSavingSettings = false;
        }
    }

    private async Task OnStatusFilterChanged(BackgroundTaskStatus? status)
    {
        _selectedStatus = status;
        await PersistFiltersAsync();
        await Task.WhenAll(LoadSummaryAsync(), RefreshTableAsync());
    }

    private async Task OnTaskTypeFilterChanged(string? taskType)
    {
        _selectedTaskType = taskType;
        await PersistFiltersAsync();
        await Task.WhenAll(LoadSummaryAsync(), RefreshTableAsync());
    }

    private async Task OnSortChanged(BackgroundTaskOrderingOption value)
    {
        if (value == _selectedSort)
        {
            return;
        }

        _selectedSort = value;
        await PersistFiltersAsync();
        await RefreshTableAsync();
    }

    private string GetSortLabel(BackgroundTaskOrderingOption option) => option switch
    {
        BackgroundTaskOrderingOption.DateDesc => L["SortDateDesc"],
        BackgroundTaskOrderingOption.DateAsc => L["SortDateAsc"],
        BackgroundTaskOrderingOption.DurationDesc => L["SortDurationDesc"],
        BackgroundTaskOrderingOption.DurationAsc => L["SortDurationAsc"],
        BackgroundTaskOrderingOption.NameAsc => L["SortNameAsc"],
        BackgroundTaskOrderingOption.NameDesc => L["SortNameDesc"],
        _ => L["SortDateDesc"]
    };

    private static (string SortBy, bool Descending) MapSortToApi(BackgroundTaskOrderingOption sort) => sort switch
    {
        BackgroundTaskOrderingOption.DateDesc => ("date", true),
        BackgroundTaskOrderingOption.DateAsc => ("date", false),
        BackgroundTaskOrderingOption.DurationDesc => ("duration", true),
        BackgroundTaskOrderingOption.DurationAsc => ("duration", false),
        BackgroundTaskOrderingOption.NameAsc => ("name", false),
        BackgroundTaskOrderingOption.NameDesc => ("name", true),
        _ => ("date", true)
    };

    private static BackgroundTaskOrderingOption MapLegacySort(string? sortBy, bool descending) =>
        (sortBy?.ToLowerInvariant(), descending) switch
        {
            ("date", true) => BackgroundTaskOrderingOption.DateDesc,
            ("date", false) => BackgroundTaskOrderingOption.DateAsc,
            ("duration", true) => BackgroundTaskOrderingOption.DurationDesc,
            ("duration", false) => BackgroundTaskOrderingOption.DurationAsc,
            ("name", false) => BackgroundTaskOrderingOption.NameAsc,
            ("name", true) => BackgroundTaskOrderingOption.NameDesc,
            _ => BackgroundTaskOrderingOption.DateDesc
        };

    private async Task CancelTaskAsync(BackgroundTaskDto task)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["CancelTitle"],
            string.Format(L["CancelMessage"], task.Name),
            yesText: L["CancelConfirm"],
            cancelText: S["Cancel"]);

        if (confirmed is not true)
            return;

        try
        {
            await BackgroundTaskService.CancelBackgroundTaskAsync(task.Id, _cts.Token);
            Snackbar.Add(L["TaskCancelled"], K7Severity.Success);
            await Task.WhenAll(RefreshTableAsync(), LoadSummaryAsync());
        }
        catch (OperationCanceledException)
        {
            // Component disposed
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private string FormatConcurrencyGroup(string? group)
    {
        if (group is null) return "-";
        if (group.StartsWith("federation:", StringComparison.Ordinal)
            && Guid.TryParse(group.AsSpan("federation:".Length), out var peerId)
            && _peerNames.TryGetValue(peerId, out var peerName))
        {
            return $"federation:{peerName}";
        }
        return group;
    }

    private async Task LoadPeerNamesAsync()
    {
        var flags = await ServerPreferencesService.GetServerFeatureFlagsAsync(_cts.Token);
        if (!flags.FederationEnabled)
            return;

        try
        {
            var peers = await FederationService.GetPeerServersAsync(_cts.Token);
            _peerNames = peers.ToDictionary(p => p.Id, p => p.Name);
        }
        catch
        {
            // Non-critical, fall back to showing GUIDs
        }
    }

    private int GetGroupLimit(string name) => _concurrencyLimits.GetValueOrDefault(name, 1);

    private void SetGroupLimit(string name, int value) => _concurrencyLimits[name] = value;

    private int GetStatusCount(BackgroundTaskStatus status) =>
        _summary?.StatusCounts.FirstOrDefault(s => s.Status == status)?.Count ?? 0;

    private string FormatStatusFilterLabel(BackgroundTaskStatus status) =>
        $"{GetStatusLabel(status)} ({GetStatusCount(status)})";

    private string GetStatusLabel(BackgroundTaskStatus status) => status switch
    {
        BackgroundTaskStatus.Pending => L["StatusPending"],
        BackgroundTaskStatus.InProgress => L["StatusInProgress"],
        BackgroundTaskStatus.WaitingForRetry => L["StatusWaitingForRetry"],
        BackgroundTaskStatus.Completed => L["StatusCompleted"],
        BackgroundTaskStatus.Failed => L["StatusFailed"],
        _ => status.ToString()
    };

    private string GetTaskTypeLabel(string taskName)
    {
        var key = $"TaskType_{taskName}";
        var localized = L[key];
        return localized.ResourceNotFound ? taskName : localized.Value;
    }

    private static string GetStatusIcon(BackgroundTaskStatus status) => status switch
    {
        BackgroundTaskStatus.Pending => "clock",
        BackgroundTaskStatus.InProgress => "play-circle",
        BackgroundTaskStatus.WaitingForRetry => "arrow-clockwise",
        BackgroundTaskStatus.Completed => "check-circle",
        BackgroundTaskStatus.Failed => "x-circle",
        _ => "circle"
    };

    private static string GetStatusIconStyle(BackgroundTaskStatus status) => status switch
    {
        BackgroundTaskStatus.InProgress => "color: var(--color-info);",
        BackgroundTaskStatus.Completed => "color: var(--color-success);",
        BackgroundTaskStatus.Failed => "color: var(--color-error);",
        BackgroundTaskStatus.WaitingForRetry => "color: var(--color-warning);",
        _ => "color: var(--color-text-muted);"
    };

    private static string GetStatusTextStyle(BackgroundTaskStatus status) => status switch
    {
        BackgroundTaskStatus.InProgress => "color: var(--color-info);",
        BackgroundTaskStatus.Completed => "color: var(--color-success);",
        BackgroundTaskStatus.Failed => "color: var(--color-error);",
        BackgroundTaskStatus.WaitingForRetry => "color: var(--color-warning);",
        _ => ""
    };

    private string FormatRelativeTime(DateTimeOffset dateTime)
    {
        var elapsed = DateTimeOffset.Now - dateTime.ToLocalTime();
        return elapsed.TotalSeconds switch
        {
            < 60 => L["TimeJustNow"],
            < 3600 => string.Format(L["TimeMinutesAgo"], (int)elapsed.TotalMinutes),
            < 86400 => string.Format(L["TimeHoursAgo"], (int)elapsed.TotalHours),
            _ => string.Format(L["TimeDaysAgo"], (int)elapsed.TotalDays)
        };
    }

    private string FormatDuration(TimeSpan duration) => duration.TotalSeconds switch
    {
        < 1 => L["DurationSubSecond"],
        < 60 => string.Format(L["DurationSeconds"], (int)duration.TotalSeconds),
        < 3600 => string.Format(L["DurationMinutes"], (int)duration.TotalMinutes, duration.Seconds),
        _ => string.Format(L["DurationHours"], (int)duration.TotalHours, duration.Minutes)
    };

    private async Task OpenDetailDialog(BackgroundTaskDto task)
    {
        var parameters = new K7DialogParameters<Dialogs.BackgroundTaskDetailDialog>
        {
            { x => x.BackgroundTask, task }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<Dialogs.BackgroundTaskDetailDialog>(task.Name, parameters, options);
    }

    private async Task DeleteTaskAsync(BackgroundTaskDto task)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteTitle"],
            string.Format(L["DeleteMessage"], task.Name),
            yesText: L["DeleteConfirm"],
            cancelText: S["Cancel"]);

        if (confirmed is not true)
            return;

        try
        {
            await BackgroundTaskService.DeleteBackgroundTaskAsync(task.Id, _cts.Token);
            Snackbar.Add(L["TaskDeleted"], K7Severity.Success);
            await Task.WhenAll(RefreshTableAsync(), LoadSummaryAsync());
        }
        catch (OperationCanceledException)
        {
            // Component disposed
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }
}

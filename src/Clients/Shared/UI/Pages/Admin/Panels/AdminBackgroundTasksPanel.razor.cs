using K7.Clients.Shared.Enums;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminBackgroundTasksPanel : IDisposable
{
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
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private K7.Clients.Shared.Services.K7HubClient K7HubClient { get; set; } = default!;

    private BackgroundTaskSettingsDto? _settings;
    private BackgroundTaskSummaryDto? _summary;
    private K7DataTable<BackgroundTaskDto>? _tableRef;
    private bool _isSavingSettings;
    private BackgroundTaskStatus? _selectedStatus;
    private string? _selectedTaskType;
    private string? _selectedSortBy;
    private bool _sortDescending = true;
    private int _workerCount;
    private Dictionary<string, int> _concurrencyLimits = new();
    private readonly CancellationTokenSource _cts = new();
    private Timer? _debounceTimer;
    private const int PageSize = 50;
    private int _tableKey;

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.BackgroundTaskUpdated += OnBackgroundTaskUpdated;
        await Task.WhenAll(LoadSettingsAsync(initial: true), LoadSummaryAsync());
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

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var tasks = Enumerable.Range(firstPage, lastPage - firstPage + 1)
                .Select(page => BackgroundTaskService.GetBackgroundTasksAsync(page, PageSize, statuses, names, _selectedSortBy, _sortDescending, cancellationToken));

            var results = await Task.WhenAll(tasks);

            var totalCount = 0;
            var allItems = new List<BackgroundTaskDto>(count);
            foreach (var result in results)
            {
                if (result?.Items is { Count: > 0 })
                {
                    totalCount = result.TotalCount ?? 0;
                    allItems.AddRange(result.Items);
                }
            }

            var offset = startIndex - (firstPage - 1) * PageSize;
            var items = allItems.Skip(offset).Take(count).ToList();

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
            _summary = await BackgroundTaskService.GetSummaryAsync(_cts.Token);
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
        await RefreshTableAsync();
    }

    private async Task OnTaskTypeFilterChanged(string? taskType)
    {
        _selectedTaskType = taskType;
        await RefreshTableAsync();
    }

    private async Task OnSortByChanged(string? sortBy)
    {
        _selectedSortBy = sortBy;
        await RefreshTableAsync();
    }

    private async Task ToggleSortDirection()
    {
        _sortDescending = !_sortDescending;
        await RefreshTableAsync();
    }

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

    private int GetGroupLimit(string name) => _concurrencyLimits.GetValueOrDefault(name, 1);

    private void SetGroupLimit(string name, int value) => _concurrencyLimits[name] = value;

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

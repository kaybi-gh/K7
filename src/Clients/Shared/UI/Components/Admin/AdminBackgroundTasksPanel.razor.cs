using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

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
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private K7.Clients.Shared.Services.K7HubClient K7HubClient { get; set; } = default!;

    private PaginatedListDto<BackgroundTaskDto>? _tasks;
    private BackgroundTaskSettingsDto? _settings;
    private BackgroundTaskSummaryDto? _summary;
    private bool _isLoadingTasks = true;
    private bool _isSavingSettings;
    private bool _settingsDrawerOpen;
    private BackgroundTaskStatus? _selectedStatus;
    private string? _selectedTaskType;
    private int _pageNumber = 1;
    private int _workerCount;
    private Dictionary<string, int> _concurrencyLimits = new();
    private readonly CancellationTokenSource _cts = new();
    private Timer? _debounceTimer;

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.BackgroundTaskUpdated += OnBackgroundTaskUpdated;
        await Task.WhenAll(LoadTasksAsync(), LoadSettingsAsync(initial: true), LoadSummaryAsync());
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
                await Task.WhenAll(LoadTasksAsync(), LoadSettingsAsync(), LoadSummaryAsync());
                StateHasChanged();
            });
        }, null, DebounceDelay, Timeout.InfiniteTimeSpan);
    }

    private async Task LoadTasksAsync()
    {
        _isLoadingTasks = true;
        try
        {
            var statuses = _selectedStatus.HasValue ? new[] { _selectedStatus.Value } : null;
            var names = _selectedTaskType is not null ? new[] { _selectedTaskType } : null;
            _tasks = await BackgroundTaskService.GetBackgroundTasksAsync(_pageNumber, 20, statuses, names, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Component disposed
        }
        catch
        {
            _tasks = null;
            Snackbar.Add(S["LoadError"], Severity.Error);
        }
        finally
        {
            _isLoadingTasks = false;
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
            Snackbar.Add(L["SettingsSaved"], Severity.Success);
            await LoadSettingsAsync(initial: true);
        }
        catch (OperationCanceledException)
        {
            // Component disposed
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
        }
        finally
        {
            _isSavingSettings = false;
        }
    }

    private async Task OnStatusFilterChanged(BackgroundTaskStatus? status)
    {
        _selectedStatus = status;
        _pageNumber = 1;
        await LoadTasksAsync();
    }

    private async Task OnTaskTypeFilterChanged(string? taskType)
    {
        _selectedTaskType = taskType;
        _pageNumber = 1;
        await LoadTasksAsync();
    }

    private async Task OnPageChanged(int page)
    {
        _pageNumber = page;
        await LoadTasksAsync();
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
        BackgroundTaskStatus.Pending => Icons.Material.Filled.Schedule,
        BackgroundTaskStatus.InProgress => Icons.Material.Filled.PlayCircle,
        BackgroundTaskStatus.WaitingForRetry => Icons.Material.Filled.Replay,
        BackgroundTaskStatus.Completed => Icons.Material.Filled.CheckCircle,
        BackgroundTaskStatus.Failed => Icons.Material.Filled.Cancel,
        _ => Icons.Material.Filled.Circle
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
        var parameters = new DialogParameters<Dialogs.BackgroundTaskDetailDialog>
        {
            { x => x.Task, task }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
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
            Snackbar.Add(L["TaskDeleted"], Severity.Success);
            await Task.WhenAll(LoadTasksAsync(), LoadSummaryAsync());
        }
        catch (OperationCanceledException)
        {
            // Component disposed
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
        }
    }
}

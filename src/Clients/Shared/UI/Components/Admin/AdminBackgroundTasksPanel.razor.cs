using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminBackgroundTasksPanel : IDisposable
{
    [Inject] private IBackgroundTaskService BackgroundTaskService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private K7.Clients.Shared.Services.K7HubClient K7HubClient { get; set; } = default!;

    private PaginatedListDto<BackgroundTaskDto>? _tasks;
    private BackgroundTaskSettingsDto? _settings;
    private bool _isLoadingTasks = true;
    private bool _isSavingSettings;
    private BackgroundTaskStatus? _selectedStatus;
    private int _pageNumber = 1;
    private int _workerCount;
    private Dictionary<string, int> _concurrencyLimits = new();

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.BackgroundTaskUpdated += OnBackgroundTaskUpdated;
        await Task.WhenAll(LoadTasksAsync(), LoadSettingsAsync(initial: true));
    }

    public void Dispose()
    {
        K7HubClient.BackgroundTaskUpdated -= OnBackgroundTaskUpdated;
    }

    private void OnBackgroundTaskUpdated()
    {
        _ = InvokeAsync(async () =>
        {
            await Task.WhenAll(LoadTasksAsync(), LoadSettingsAsync());
            StateHasChanged();
        });
    }

    private async Task LoadTasksAsync()
    {
        _isLoadingTasks = true;
        try
        {
            var statuses = _selectedStatus.HasValue ? new[] { _selectedStatus.Value } : null;
            _tasks = await BackgroundTaskService.GetBackgroundTasksAsync(_pageNumber, 20, statuses);
        }
        catch
        {
            _tasks = null;
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
            _settings = await BackgroundTaskService.GetSettingsAsync();
            if (initial)
            {
                _workerCount = _settings.WorkerCount;
                _concurrencyLimits = _settings.ConcurrencyGroups.ToDictionary(g => g.Name, g => g.Limit);
            }
        }
        catch
        {
            _settings = null;
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
            await BackgroundTaskService.UpdateSettingsAsync(request);
            Snackbar.Add(L["SettingsSaved"], Severity.Success);
            await LoadSettingsAsync(initial: true);
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

    private static Color GetStatusColor(BackgroundTaskStatus status) => status switch
    {
        BackgroundTaskStatus.Pending => Color.Info,
        BackgroundTaskStatus.InProgress => Color.Warning,
        BackgroundTaskStatus.WaitingForRetry => Color.Secondary,
        BackgroundTaskStatus.Completed => Color.Success,
        BackgroundTaskStatus.Failed => Color.Error,
        _ => Color.Default
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
            await BackgroundTaskService.DeleteBackgroundTaskAsync(task.Id);
            Snackbar.Add(L["TaskDeleted"], Severity.Success);
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
        }
    }
}

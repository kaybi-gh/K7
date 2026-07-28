using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Pages.Admin.Panels;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class BackgroundTaskSettingsDialog : IDisposable
{
    private const int MaxLaneLimit = 32;
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Inject] private IBackgroundTaskService BackgroundTaskService { get; set; } = default!;
    [Inject] private K7.Clients.Shared.Services.K7HubClient K7HubClient { get; set; } = default!;
    [Inject] private IStringLocalizer<AdminBackgroundTasksPanel> EnumL { get; set; } = default!;

    private BackgroundTaskSettingsDto? _settings;
    private bool _isLoading = true;
    private int _workerCount;
    private Dictionary<BackgroundTaskLane, int> _laneLimits = new();
    private readonly CancellationTokenSource _cts = new();
    private Timer? _debounceTimer;

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.BackgroundTaskUpdated += OnBackgroundTaskUpdated;
        await LoadSettingsAsync(initial: true);
        _isLoading = false;
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
                await LoadSettingsAsync();
                StateHasChanged();
            });
        }, null, DebounceDelay, Timeout.InfiniteTimeSpan);
    }

    private async Task LoadSettingsAsync(bool initial = false)
    {
        try
        {
            _settings = await BackgroundTaskService.GetSettingsAsync(_cts.Token);
            if (initial)
            {
                _workerCount = _settings.WorkerCount;
                _laneLimits = _settings.Lanes.ToDictionary(l => l.Lane, l => l.Limit);
            }
        }
        catch (OperationCanceledException)
        {
            // Dialog closed
        }
        catch
        {
            if (initial)
            {
                _settings = null;
            }
        }
    }

    private string GetLaneLabel(BackgroundTaskLane lane) =>
        BackgroundTaskLabelHelper.GetLaneLabel(EnumL, lane);

    private int GetLaneLimit(BackgroundTaskLane lane) => _laneLimits.GetValueOrDefault(lane);

    private void SetLaneLimit(BackgroundTaskLane lane, int value) =>
        _laneLimits[lane] = Math.Clamp(value, 0, MaxLaneLimit);

    private void Cancel() => Dialog.Cancel();

    private void Submit()
    {
        var request = new UpdateBackgroundTaskSettingsRequest
        {
            WorkerCount = _workerCount,
            LaneLimits = _laneLimits
        };
        Dialog.Close(K7DialogResult.Ok(request));
    }
}

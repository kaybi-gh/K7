using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class BackgroundTaskSettingsDialog : IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Inject] private IBackgroundTaskService BackgroundTaskService { get; set; } = default!;
    [Inject] private IFederationService FederationService { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
    [Inject] private K7.Clients.Shared.Services.K7HubClient K7HubClient { get; set; } = default!;

    private BackgroundTaskSettingsDto? _settings;
    private bool _isLoading = true;
    private int _workerCount;
    private Dictionary<string, int> _concurrencyLimits = new();
    private Dictionary<Guid, string> _peerNames = new();
    private readonly CancellationTokenSource _cts = new();
    private Timer? _debounceTimer;

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.BackgroundTaskUpdated += OnBackgroundTaskUpdated;
        await Task.WhenAll(LoadSettingsAsync(initial: true), LoadPeerNamesAsync());
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
                _concurrencyLimits = _settings.ConcurrencyGroups.ToDictionary(g => g.Name, g => g.Limit);
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

    private async Task LoadPeerNamesAsync()
    {
        var flags = await ServerPreferencesService.GetServerFeatureFlagsAsync();
        if (!flags.FederationEnabled)
        {
            return;
        }

        try
        {
            var peers = await FederationService.GetPeerServersAsync();
            _peerNames = peers.ToDictionary(p => p.Id, p => p.Name);
        }
        catch
        {
            // Non-critical, fall back to showing GUIDs
        }
    }

    private string FormatConcurrencyGroup(string? group)
    {
        if (group is null)
        {
            return "-";
        }

        if (group.StartsWith("federation:", StringComparison.Ordinal)
            && Guid.TryParse(group.AsSpan("federation:".Length), out var peerId)
            && _peerNames.TryGetValue(peerId, out var peerName))
        {
            return $"federation:{peerName}";
        }

        return group;
    }

    private int GetGroupLimit(string name) => _concurrencyLimits.GetValueOrDefault(name, 1);

    private void SetGroupLimit(string name, int value) => _concurrencyLimits[name] = value;

    private void Cancel() => Dialog.Cancel();

    private void Submit()
    {
        var request = new UpdateBackgroundTaskSettingsRequest
        {
            WorkerCount = _workerCount,
            ConcurrencyLimits = _concurrencyLimits
        };
        Dialog.Close(K7DialogResult.Ok(request));
    }
}

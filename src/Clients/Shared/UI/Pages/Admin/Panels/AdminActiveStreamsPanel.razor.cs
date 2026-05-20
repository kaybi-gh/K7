using K7.Clients.Shared.Services;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminActiveStreamsPanel : IDisposable
{
    private IReadOnlyList<ActiveStreamDto>? _streams;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        HubClient.ActiveStreamsUpdated += OnActiveStreamsUpdated;

        await FetchStreamsAsync();

        try
        {
            await HubClient.JoinAdminStreamsGroupAsync();
        }
        catch
        {
            // Fallback: if hub not connected yet, we still have initial fetch
        }
    }

    private void OnActiveStreamsUpdated(IReadOnlyList<ActiveStreamDto> streams)
    {
        InvokeAsync(() =>
        {
            _streams = streams;
            _loading = false;
            StateHasChanged();
        });
    }

    private async Task FetchStreamsAsync()
    {
        _loading = _streams is null;

        try
        {
            _streams = await K7ServerService.GetActiveStreamsAsync();
        }
        catch
        {
            _streams = null;
        }

        _loading = false;
    }

    public void Dispose()
    {
        HubClient.ActiveStreamsUpdated -= OnActiveStreamsUpdated;

        _ = HubClient.LeaveAdminStreamsGroupAsync();
    }
}

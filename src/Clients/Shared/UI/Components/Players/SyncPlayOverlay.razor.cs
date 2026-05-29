using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class SyncPlayOverlay : IDisposable
{
    [Parameter] public bool Compact { get; set; }

    private bool _showParticipants;

    protected override void OnInitialized()
    {
        SyncPlay.GroupUpdated += OnGroupUpdated;
    }

    private async Task LeaveGroup()
    {
        await SyncPlay.LeaveGroupAsync();
    }

    private void OnGroupUpdated() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        SyncPlay.GroupUpdated -= OnGroupUpdated;
    }
}

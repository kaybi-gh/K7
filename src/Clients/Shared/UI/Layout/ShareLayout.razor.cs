using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Layout;

public partial class ShareLayout
{
    [Inject] private ISharePlaybackScope PlaybackScope { get; set; } = default!;

    // Before the players below are created: they ask for settings a visitor does not have.
    protected override void OnInitialized() => PlaybackScope.BeginVisitorPage();

    public void Dispose()
    {
        PlaybackScope.EndVisitorPage();
        GC.SuppressFinalize(this);
    }
}

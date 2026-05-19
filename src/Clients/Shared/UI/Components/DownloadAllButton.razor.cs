using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class DownloadAllButton : ComponentBase
{
    [Parameter, EditorRequired]
    public IReadOnlyList<DownloadRequest> Items { get; set; } = [];

    [Parameter]
    public string Variant { get; set; } = "text";

    [Parameter]
    public string Size { get; set; } = "md";

    private bool _isEnqueuing;

    private async Task DownloadAllAsync()
    {
        if (_isEnqueuing || Items.Count == 0) return;
        _isEnqueuing = true;

        var count = 0;
        foreach (var item in Items)
        {
            await DownloadManager.EnqueueAsync(item);
            count++;
        }

        Snackbar.Add(string.Format(L["DownloadAllQueued"], count), K7Severity.Info);
        _isEnqueuing = false;
        StateHasChanged();
    }
}

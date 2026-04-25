using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7SnackbarHost : IDisposable
{
    [Inject] private IK7Snackbar SnackbarService { get; set; } = default!;

    private readonly List<K7SnackbarMessage> _messages = [];

    protected override void OnInitialized()
    {
        SnackbarService.OnAdd += OnAdd;
    }

    private void OnAdd(K7SnackbarMessage msg)
    {
        _messages.Add(msg);
        InvokeAsync(StateHasChanged);
        _ = AutoRemoveAsync(msg);
    }

    private async Task AutoRemoveAsync(K7SnackbarMessage msg)
    {
        await Task.Delay(4000);
        Remove(msg);
    }

    private void Remove(K7SnackbarMessage msg)
    {
        _messages.Remove(msg);
        InvokeAsync(StateHasChanged);
    }

    private static string GetIcon(K7Severity severity) => severity switch
    {
        K7Severity.Success => "check-circle",
        K7Severity.Error => "warning-circle",
        K7Severity.Warning => "warning",
        K7Severity.Info => "info",
        _ => "bell"
    };

    public void Dispose()
    {
        SnackbarService.OnAdd -= OnAdd;
    }
}

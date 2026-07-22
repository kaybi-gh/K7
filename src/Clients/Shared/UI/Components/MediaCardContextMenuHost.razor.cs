using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class MediaCardContextMenuHost : IDisposable
{
    private MediaCardContextMenuRequest? _request;
    private bool _open;

    protected override void OnInitialized()
    {
        ContextMenuService.Changed += OnServiceChanged;
        _request = ContextMenuService.Current;
        _open = _request is not null;
    }

    private void OnServiceChanged() =>
        InvokeAsync(HandleServiceChangedAsync).FireAndForget();

    private async Task HandleServiceChangedAsync()
    {
        var current = ContextMenuService.Current;
        if (current is null)
        {
            if (!_open && _request is null)
                return;

            _open = false;
            StateHasChanged();
            await Task.Yield();
            _request = null;
            StateHasChanged();
            return;
        }

        if (_open && _request?.OwnerId != current.OwnerId)
        {
            _open = false;
            StateHasChanged();
            await Task.Yield();
        }

        _request = current;
        _open = true;
        StateHasChanged();
    }

    private void OnOpenChanged(bool open)
    {
        _open = open;
        if (!open && ContextMenuService.Current is not null)
            ContextMenuService.Close();
    }

    public void Dispose()
    {
        ContextMenuService.Changed -= OnServiceChanged;
        try
        {
            _ = JS.InvokeVoidAsync("K7.clearMenuPositionAnchor");
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSException)
        {
        }
    }
}

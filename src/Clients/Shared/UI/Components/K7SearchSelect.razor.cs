using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7SearchSelect : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public string Placeholder { get; set; } = "";
    [Parameter] public string Variant { get; set; } = "outlined";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public int DebounceInterval { get; set; } = 300;
    [Parameter] public int MinSearchLength { get; set; }
    [Parameter] public bool CommitOnSelectOnly { get; set; }
    [Parameter] public EventCallback<string?> OnDebouncedCommit { get; set; }
    [Parameter] public Func<string, CancellationToken, Task<IReadOnlyList<string>>>? SearchAsync { get; set; }

    private bool _open;
    private bool _loading;
    private bool _editing;
    private bool _disposed;
    private bool _scrollToHighlighted;
    private bool _scrollIntoMenuView;
    private bool _editingListenerBound;
    private int _highlightedIndex = -1;
    private IReadOnlyList<string> _suggestions = [];
    private CancellationTokenSource? _searchCts;
    private ElementReference _root;
    private ElementReference _dropdown;
    private DotNetObjectReference<K7SearchSelect>? _dotNetRef;

    private async Task OnInputChanged(string? value)
    {
        if (!_editing)
            return;

        Value = value;
        if (!CommitOnSelectOnly)
            await ValueChanged.InvokeAsync(value);
    }

    private async Task OnDebouncedSearch(string? value)
    {
        if (_disposed || !_editing)
            return;

        Value = value;
        await ValueChanged.InvokeAsync(value);
        _highlightedIndex = -1;

        if (SearchAsync is null)
        {
            _suggestions = [];
            _open = false;
            if (!CommitOnSelectOnly && OnDebouncedCommit.HasDelegate)
                await OnDebouncedCommit.InvokeAsync(null);
            if (!_disposed)
                StateHasChanged();
            return;
        }

        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length < MinSearchLength)
        {
            _suggestions = [];
            _open = false;
            _searchCts?.Cancel();
            if (!_disposed)
                StateHasChanged();
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _loading = true;
        _open = true;
        if (!_disposed)
            StateHasChanged();

        try
        {
            _suggestions = await SearchAsync(trimmed, token);
            if (_disposed || token.IsCancellationRequested)
                return;

            _open = _suggestions.Count > 0;
            _highlightedIndex = _open ? 0 : -1;
            _scrollToHighlighted = _open;
            _scrollIntoMenuView = _open;

            if (!CommitOnSelectOnly)
            {
                if (OnDebouncedCommit.HasDelegate)
                    await OnDebouncedCommit.InvokeAsync(trimmed);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!_disposed && !token.IsCancellationRequested)
            {
                _loading = false;
                StateHasChanged();
            }
        }
    }

    private async Task SelectSuggestionAsync(string suggestion)
    {
        if (_disposed)
            return;

        Value = suggestion;
        await ValueChanged.InvokeAsync(suggestion);
        if (OnDebouncedCommit.HasDelegate)
            await OnDebouncedCommit.InvokeAsync(suggestion);
        await EndEditingAsync();
    }

    private async Task OnInputKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" && !_editing)
        {
            await BeginEditingAsync();
            return;
        }

        if (!_editing)
            return;

        if (!_open || _loading || _suggestions.Count == 0)
        {
            if (e.Key is "Escape" && _open)
            {
                CloseDropdown();
                if (!_disposed)
                    StateHasChanged();
            }

            return;
        }

        switch (e.Key)
        {
            case "ArrowDown":
                _highlightedIndex = Math.Min(_highlightedIndex + 1, _suggestions.Count - 1);
                if (_highlightedIndex < 0)
                    _highlightedIndex = 0;
                _scrollToHighlighted = true;
                if (!_disposed)
                    StateHasChanged();
                break;
            case "ArrowUp":
                _highlightedIndex = Math.Max(_highlightedIndex - 1, 0);
                _scrollToHighlighted = true;
                if (!_disposed)
                    StateHasChanged();
                break;
            case "Enter":
                if (_highlightedIndex >= 0 && _highlightedIndex < _suggestions.Count)
                    await SelectSuggestionAsync(_suggestions[_highlightedIndex]);
                break;
            case "Escape":
                CloseDropdown();
                if (!_disposed)
                    StateHasChanged();
                break;
        }
    }

    private void OnFocus(FocusEventArgs _)
    {
        if (_editing && _suggestions.Count > 0)
            _open = true;
    }

    private Task OnInputClick(MouseEventArgs _) =>
        !_editing ? BeginEditingAsync() : Task.CompletedTask;

    private Task OnFocusOut(FocusEventArgs _) => CloseDropdownIfFocusLeftAsync();

    private async Task CloseDropdownIfFocusLeftAsync()
    {
        await Task.Yield();
        if (_disposed)
            return;

        try
        {
            if (await JS.InvokeAsync<bool>("K7.isFocusWithin", _root))
                return;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }

        await EndEditingAsync();
    }

    private async Task BeginEditingAsync()
    {
        if (_editing)
            return;

        _editing = true;
        if (!_disposed)
            StateHasChanged();

        await OnDebouncedSearch(Value);
    }

    private async Task EndEditingAsync()
    {
        _editing = false;
        _suggestions = [];
        CloseDropdown();
        try
        {
            await JS.InvokeVoidAsync("K7.unbindSearchSelectMenuDismiss", _root);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }

        if (!_disposed)
            await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnSpatialEditStarted() => BeginEditingAsync();

    [JSInvokable]
    public Task OnSpatialEditEnded() => EndEditingAsync();

    private void CloseDropdown()
    {
        _open = false;
        _highlightedIndex = -1;
        _scrollToHighlighted = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_editingListenerBound && SearchAsync is not null)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            try
            {
                await JS.InvokeVoidAsync("K7.bindSearchSelectEditing", _root, _dotNetRef);
                _editingListenerBound = true;
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
            {
            }
        }

        if (_scrollIntoMenuView && _open && !_disposed)
        {
            _scrollIntoMenuView = false;
            try
            {
                await JS.InvokeVoidAsync("K7.scrollSearchSelectIntoMenuView", _root);
                await JS.InvokeVoidAsync("K7.bindSearchSelectMenuDismiss", _root);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
            {
            }
        }

        if (!_scrollToHighlighted || _highlightedIndex < 0 || _disposed)
            return;

        _scrollToHighlighted = false;

        try
        {
            await JS.InvokeVoidAsync("K7.scrollSearchSelectOptionIntoView", _dropdown, _highlightedIndex);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        try
        {
            _ = JS.InvokeVoidAsync("K7.unbindSearchSelectMenuDismiss", _root);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }

        _dotNetRef?.Dispose();
        return ValueTask.CompletedTask;
    }
}

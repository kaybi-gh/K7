using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

public partial class K7SearchSelect : ComponentBase, IDisposable
{
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public string Placeholder { get; set; } = "";
    [Parameter] public string Variant { get; set; } = "outlined";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public int DebounceInterval { get; set; } = 300;
    [Parameter] public bool CommitOnSelectOnly { get; set; }
    [Parameter] public EventCallback<string?> OnDebouncedCommit { get; set; }
    [Parameter] public Func<string, CancellationToken, Task<IReadOnlyList<string>>>? SearchAsync { get; set; }

    private bool _open;
    private bool _loading;
    private IReadOnlyList<string> _suggestions = [];
    private CancellationTokenSource? _searchCts;
    private ElementReference _root;

    private async Task OnInputChanged(string? value)
    {
        Value = value;
        if (!CommitOnSelectOnly)
            await ValueChanged.InvokeAsync(value);
    }

    private async Task OnDebouncedSearch(string? value)
    {
        if (SearchAsync is null || string.IsNullOrWhiteSpace(value))
        {
            _suggestions = [];
            _open = false;
            if (CommitOnSelectOnly && OnDebouncedCommit.HasDelegate)
                await OnDebouncedCommit.InvokeAsync(null);
            StateHasChanged();
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _loading = true;
        _open = true;
        StateHasChanged();

        try
        {
            _suggestions = await SearchAsync(value.Trim(), token);
            if (!token.IsCancellationRequested)
                _open = _suggestions.Count > 0;

            if (!token.IsCancellationRequested && CommitOnSelectOnly && OnDebouncedCommit.HasDelegate)
                await OnDebouncedCommit.InvokeAsync(value.Trim());
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!token.IsCancellationRequested)
                _loading = false;
            StateHasChanged();
        }
    }

    private async Task SelectSuggestionAsync(string suggestion)
    {
        Value = suggestion;
        await ValueChanged.InvokeAsync(suggestion);
        if (CommitOnSelectOnly && OnDebouncedCommit.HasDelegate)
            await OnDebouncedCommit.InvokeAsync(suggestion);
        _open = false;
        _suggestions = [];
    }

    private void OnFocus(FocusEventArgs _)
    {
        if (_suggestions.Count > 0)
            _open = true;
    }

    private void CloseDropdown()
    {
        _open = false;
    }

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}

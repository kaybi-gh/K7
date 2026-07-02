using K7.Clients.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class BrowseView<TItem> : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public IList<TItem>? Items { get; set; }
    [Parameter] public ItemsProviderDelegate<TItem>? ItemsProvider { get; set; }
    [Parameter] public bool Loading { get; set; }
    [Parameter] public bool HasMore { get; set; }
    [Parameter] public Func<Task>? OnLoadMore { get; set; }

    [Parameter] public RenderFragment<TItem>? GridTemplate { get; set; }
    [Parameter] public RenderFragment<TItem>? ListTemplate { get; set; }
    [Parameter] public RenderFragment? TableHeaderContent { get; set; }
    [Parameter] public RenderFragment<TItem>? TableRowTemplate { get; set; }
    [Parameter] public RenderFragment? TableContent { get; set; }
    [Parameter] public RenderFragment? ToolbarContent { get; set; }
    [Parameter] public RenderFragment? ToolbarSecondaryContent { get; set; }
    [Parameter] public RenderFragment? LoadingContent { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public RenderFragment? GridPlaceholder { get; set; }
    [Parameter] public RenderFragment? ListPlaceholder { get; set; }
    [Parameter] public EventCallback OnColumnPickerRequested { get; set; }

    [Parameter] public IReadOnlyList<string>? JumpIndexLabels { get; set; }
    [Parameter] public EventCallback<string> OnJumpRequested { get; set; }

    [Parameter] public string PersistenceKey { get; set; } = "default";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public BrowseViewMode DefaultMode { get; set; } = BrowseViewMode.Grid;
    [Parameter] public int DefaultItemWidth { get; set; } = 160;
    [Parameter] public int DefaultSpacing { get; set; } = 6;
    [Parameter] public float ListItemHeight { get; set; } = 64;
    [Parameter] public float? GridItemAspectRatio { get; set; } = 1.5f;
    [Parameter] public int GridFooterHeight { get; set; } = 44;
    [Parameter] public int OverscanCount { get; set; } = 10;

    private K7VirtualGrid<TItem>? _gridComponentRef;
    private K7VirtualList<TItem>? _listComponentRef;
    private ElementReference _sentinelRef;
    private IJSObjectReference? _module;
    private DotNetObjectReference<BrowseView<TItem>>? _dotnetRef;

    private BrowseViewMode _currentMode;
    private List<BrowseViewMode> _availableModes = [];
    private List<ButtonGroupOption<BrowseViewMode>> _modeOptions = [];
    private bool _hasColumnPicker;
    private bool _initialized;
    private bool _isMobileViewport;
    private int _itemWidth;
    private int _spacing;
    private bool _disposed;
    private bool _sentinelObserving;
    private bool _loadingMore;
    private int? _totalItemCount;

    protected override void OnInitialized()
    {
        _currentMode = DefaultMode;
        _itemWidth = DefaultItemWidth;
        _spacing = DefaultSpacing;
        _availableModes = BuildAvailableModes();
        _modeOptions = BuildModeOptions();
        _hasColumnPicker = OnColumnPickerRequested.HasDelegate;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/browseView.js");

            _dotnetRef ??= DotNetObjectReference.Create(this);

            var saved = await _module.InvokeAsync<BrowseViewSettings?>("getSettings", PersistenceKey);
            if (saved is not null)
            {
                _currentMode = saved.Mode;
            }

            _isMobileViewport = await _module.InvokeAsync<bool>("observeViewport", _dotnetRef);
            ApplyMobileModeRestrictions();

            _initialized = true;
            StateHasChanged();
            return;
        }

        if (!_sentinelObserving && HasMore && OnLoadMore is not null && !Loading && Items is { Count: > 0 })
        {
            _sentinelObserving = true;
            await StartObservingSentinel();
        }
    }

    protected override void OnParametersSet()
    {
        ApplyMobileModeRestrictions();
        _hasColumnPicker = OnColumnPickerRequested.HasDelegate;

        if (Items is not null)
        {
            _totalItemCount = Items.Count;
        }
        else if (TableContent is not null && _currentMode is BrowseViewMode.Table)
        {
            _totalItemCount = null;
        }
    }

    private async Task SetViewModeAsync(BrowseViewMode mode)
    {
        if (mode == _currentMode) return;
        _currentMode = mode;
        await SaveSettingsAsync();
    }

    public async Task RefreshAsync()
    {
        switch (_currentMode)
        {
            case BrowseViewMode.Grid when _gridComponentRef is not null:
                await _gridComponentRef.RefreshAsync();
                break;
            case BrowseViewMode.List when _listComponentRef is not null:
                await _listComponentRef.RefreshAsync();
                break;
        }
    }

    public void ScrollToItemIndex(int itemIndex)
    {
        switch (_currentMode)
        {
            case BrowseViewMode.Grid when _gridComponentRef is not null:
                _gridComponentRef.ScrollToItemIndex(itemIndex);
                break;
            case BrowseViewMode.List when _listComponentRef is not null:
                _ = _listComponentRef.ScrollToItemIndex(itemIndex);
                break;
        }
    }

    private bool IsEmpty => _totalItemCount is 0 && !Loading && _initialized;

    private bool UseProvider => ItemsProvider is not null;

    private async ValueTask<ItemsProviderResult<TItem>> WrappedItemsProvider(ItemsProviderRequest request)
    {
        if (ItemsProvider is null) return default;

        var result = await ItemsProvider(request);
        var prevCount = _totalItemCount;
        _totalItemCount = result.TotalItemCount;

        if (_totalItemCount != prevCount)
        {
            await InvokeAsync(StateHasChanged);
        }

        return result;
    }

    [JSInvokable]
    public Task OnViewportChanged(bool isMobile)
    {
        if (_disposed) return Task.CompletedTask;

        if (_isMobileViewport == isMobile) return Task.CompletedTask;

        _isMobileViewport = isMobile;
        ApplyMobileModeRestrictions();
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnSentinelVisible()
    {
        if (_disposed || _loadingMore || OnLoadMore is null || !HasMore) return;

        _loadingMore = true;
        try
        {
            await InvokeAsync(async () =>
            {
                await OnLoadMore();
                StateHasChanged();
            });
        }
        finally
        {
            _loadingMore = false;
        }
    }

    private async Task StartObservingSentinel()
    {
        if (_module is null) return;
        _dotnetRef ??= DotNetObjectReference.Create(this);

        await _module.InvokeVoidAsync("observeSentinel", _sentinelRef, _dotnetRef);
    }

    private async Task SaveSettingsAsync()
    {
        if (_module is null) return;
        var settings = new BrowseViewSettings
        {
            Mode = _currentMode
        };
        await _module.InvokeVoidAsync("saveSettings", PersistenceKey, settings);
    }

    private void ApplyMobileModeRestrictions()
    {
        _availableModes = BuildAvailableModes();
        _modeOptions = BuildModeOptions();

        if (_isMobileViewport && _currentMode is BrowseViewMode.Table)
        {
            _currentMode = _availableModes.Contains(BrowseViewMode.Grid)
                ? BrowseViewMode.Grid
                : _availableModes.Count > 0 ? _availableModes[0] : DefaultMode;
            _ = SaveSettingsAsync();
        }
        else if (!_availableModes.Contains(_currentMode) && _availableModes.Count > 0)
        {
            _currentMode = _availableModes[0];
        }
    }

    private List<BrowseViewMode> BuildAvailableModes()
    {
        var modes = new List<BrowseViewMode>();
        if (GridTemplate is not null) modes.Add(BrowseViewMode.Grid);
        if (!_isMobileViewport
            && (TableContent is not null || (TableHeaderContent is not null && TableRowTemplate is not null)))
        {
            modes.Add(BrowseViewMode.Table);
        }

        if (ListTemplate is not null) modes.Add(BrowseViewMode.List);
        return modes;
    }

    private List<ButtonGroupOption<BrowseViewMode>> BuildModeOptions() =>
        _availableModes.Select(m => new ButtonGroupOption<BrowseViewMode>(m, Icon: GetModeIcon(m))).ToList();

    private async Task OnColumnPickerClicked()
    {
        await OnColumnPickerRequested.InvokeAsync();
    }

    private static string GetModeIcon(BrowseViewMode mode) => mode switch
    {
        BrowseViewMode.Grid => Phosphor.SquaresFour,
        BrowseViewMode.Table => Phosphor.Rows,
        BrowseViewMode.List => Phosphor.ListBullets,
        _ => Phosphor.SquaresFour
    };

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        if (_module is not null)
        {
            try
            {
                if (_dotnetRef is not null)
                {
                    await _module.InvokeVoidAsync("disposeViewport", _dotnetRef);
                }

                await _module.InvokeVoidAsync("disposeSentinel");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
        _dotnetRef?.Dispose();
    }

    private sealed class BrowseViewSettings
    {
        public BrowseViewMode Mode { get; set; }
    }
}

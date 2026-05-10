using K7.Clients.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class BrowseView<TItem> : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public IList<TItem> Items { get; set; } = [];
    [Parameter] public bool Loading { get; set; }
    [Parameter] public bool HasMore { get; set; }
    [Parameter] public Func<Task>? OnLoadMore { get; set; }

    [Parameter] public RenderFragment<TItem>? GridTemplate { get; set; }
    [Parameter] public RenderFragment<TItem>? ListTemplate { get; set; }
    [Parameter] public RenderFragment? TableHeaderContent { get; set; }
    [Parameter] public RenderFragment<TItem>? TableRowTemplate { get; set; }
    [Parameter] public RenderFragment? ToolbarContent { get; set; }
    [Parameter] public RenderFragment? LoadingContent { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }

    [Parameter] public string PersistenceKey { get; set; } = "default";
    [Parameter] public BrowseViewMode DefaultMode { get; set; } = BrowseViewMode.Grid;
    [Parameter] public int DefaultItemWidth { get; set; } = 160;
    [Parameter] public int DefaultSpacing { get; set; } = 6;
    [Parameter] public float ListItemHeight { get; set; } = 64;
    [Parameter] public float GridItemAspectRatio { get; set; } = 1.5f;

    private ElementReference _gridRef;
    private ElementReference _sentinelRef;
    private IJSObjectReference? _module;
    private DotNetObjectReference<BrowseView<TItem>>? _dotnetRef;

    private BrowseViewMode _currentMode;
    private List<BrowseViewMode> _availableModes = [];
    private bool _settingsOpen;
    private int _itemWidth;
    private int _spacing;
    private int _containerWidth;
    private int _lastColumnCount;
    private float _estimatedRowHeight = 300;

    private List<List<TItem>> _rows = [];
    private bool _observingGrid;
    private bool _observingSentinel;

    protected override void OnInitialized()
    {
        _currentMode = DefaultMode;
        _itemWidth = DefaultItemWidth;
        _spacing = DefaultSpacing;
        _availableModes = BuildAvailableModes();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/browseView.js");

            var saved = await _module.InvokeAsync<BrowseViewSettings?>("getSettings", PersistenceKey);
            if (saved is not null)
            {
                _currentMode = saved.Mode;
                _itemWidth = saved.ItemWidth;
                _spacing = saved.Spacing;
            }

            RebuildRows();
            StateHasChanged();
            return;
        }

        if (!_observingGrid && _currentMode is BrowseViewMode.Grid && !Loading && Items is { Count: > 0 })
        {
            _observingGrid = true;
            await StartObservingGridWidth();
        }

        if (!_observingSentinel && HasMore && OnLoadMore is not null && !Loading && Items is { Count: > 0 })
        {
            _observingSentinel = true;
            await StartObservingSentinel();
        }
    }

    protected override void OnParametersSet()
    {
        _availableModes = BuildAvailableModes();

        if (!_availableModes.Contains(_currentMode) && _availableModes.Count > 0)
        {
            _currentMode = _availableModes[0];
        }

        _observingGrid = false;
        _observingSentinel = false;
        RebuildRows();
    }

    [JSInvokable]
    public void OnContainerWidthChanged(int width)
    {
        if (width == _containerWidth) return;
        _containerWidth = width;

        var newCols = CalculateColumnCount();
        if (newCols == _lastColumnCount) return;

        RebuildRows();
        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnSentinelVisible()
    {
        if (OnLoadMore is not null && HasMore)
        {
            await InvokeAsync(async () =>
            {
                await OnLoadMore();
                StateHasChanged();
            });
        }
    }

    private async Task SetViewModeAsync(BrowseViewMode mode)
    {
        if (mode == _currentMode) return;
        _currentMode = mode;

        if (mode is not BrowseViewMode.Grid)
        {
            _observingGrid = false;
        }

        RebuildRows();
        await SaveSettingsAsync();
    }

    private async Task OnItemWidthChanged(int value)
    {
        _itemWidth = value;
        RebuildRows();
        UpdateEstimatedRowHeight();
        await SaveSettingsAsync();
    }

    private async Task OnSpacingChanged(int value)
    {
        _spacing = value;
        RebuildRows();
        await SaveSettingsAsync();
    }

    private void RebuildRows()
    {
        if (Items is null || Items.Count == 0 || _currentMode is not BrowseViewMode.Grid)
        {
            _rows = [];
            return;
        }

        var cols = CalculateColumnCount();
        _lastColumnCount = cols;
        _rows = Items
            .Chunk(cols)
            .Select(chunk => chunk.ToList())
            .ToList();

        UpdateEstimatedRowHeight();
    }

    private int CalculateColumnCount()
    {
        if (_containerWidth <= 0) return 4;
        var cols = (_containerWidth + _spacing) / (_itemWidth + _spacing);
        return Math.Max(cols, 1);
    }

    private void UpdateEstimatedRowHeight()
    {
        _estimatedRowHeight = _itemWidth * GridItemAspectRatio + _spacing + 40;
    }

    private async Task StartObservingGridWidth()
    {
        if (_module is null) return;
        _dotnetRef ??= DotNetObjectReference.Create(this);

        await _module.InvokeVoidAsync("observeContainerWidth", _gridRef, _dotnetRef);
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
            Mode = _currentMode,
            ItemWidth = _itemWidth,
            Spacing = _spacing
        };
        await _module.InvokeVoidAsync("saveSettings", PersistenceKey, settings);
    }

    private List<BrowseViewMode> BuildAvailableModes()
    {
        var modes = new List<BrowseViewMode>();
        if (GridTemplate is not null) modes.Add(BrowseViewMode.Grid);
        if (TableHeaderContent is not null && TableRowTemplate is not null) modes.Add(BrowseViewMode.Table);
        if (ListTemplate is not null) modes.Add(BrowseViewMode.List);
        return modes;
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
        if (_module is not null)
        {
            try
            {
                if (_observingGrid)
                {
                    await _module.InvokeVoidAsync("dispose", _gridRef);
                }
                if (_observingSentinel)
                {
                    await _module.InvokeVoidAsync("disposeSentinel", _sentinelRef);
                }
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
        public int ItemWidth { get; set; }
        public int Spacing { get; set; }
    }
}

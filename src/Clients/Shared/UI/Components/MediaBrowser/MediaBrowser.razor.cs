using K7.Clients.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.MediaBrowser;

public partial class MediaBrowser<TItem> : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public IList<TItem> Items { get; set; } = [];
    [Parameter] public bool Loading { get; set; }

    [Parameter] public RenderFragment<TItem>? GridTemplate { get; set; }
    [Parameter] public RenderFragment<TItem>? ListTemplate { get; set; }
    [Parameter] public RenderFragment? TableHeaderContent { get; set; }
    [Parameter] public RenderFragment<TItem>? TableRowTemplate { get; set; }
    [Parameter] public RenderFragment? ToolbarContent { get; set; }
    [Parameter] public RenderFragment? LoadingContent { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }

    [Parameter] public string PersistenceKey { get; set; } = "default";
    [Parameter] public MediaBrowserViewMode DefaultMode { get; set; } = MediaBrowserViewMode.Grid;
    [Parameter] public int DefaultItemWidth { get; set; } = 160;
    [Parameter] public int DefaultSpacing { get; set; } = 6;
    [Parameter] public float ListItemHeight { get; set; } = 64;
    [Parameter] public float GridItemAspectRatio { get; set; } = 1.5f;

    private ElementReference _gridRef;
    private IJSObjectReference? _module;
    private DotNetObjectReference<MediaBrowser<TItem>>? _dotnetRef;

    private MediaBrowserViewMode _currentMode;
    private List<MediaBrowserViewMode> _availableModes = [];
    private bool _settingsOpen;
    private int _itemWidth;
    private int _spacing;
    private int _containerWidth;
    private float _estimatedRowHeight = 300;

    private List<List<TItem>> _rows = [];

    protected override void OnInitialized()
    {
        _currentMode = DefaultMode;
        _itemWidth = DefaultItemWidth;
        _spacing = DefaultSpacing;
        _availableModes = BuildAvailableModes();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/K7.Clients.Shared.UI/js/mediaBrowser.js");

        var saved = await _module.InvokeAsync<MediaBrowserSettings?>("getSettings", PersistenceKey);
        if (saved is not null)
        {
            _currentMode = saved.Mode;
            _itemWidth = saved.ItemWidth;
            _spacing = saved.Spacing;
        }

        if (_currentMode is MediaBrowserViewMode.Grid)
        {
            await StartObservingGridWidth();
        }

        RebuildRows();
        StateHasChanged();
    }

    protected override void OnParametersSet()
    {
        _availableModes = BuildAvailableModes();

        if (!_availableModes.Contains(_currentMode) && _availableModes.Count > 0)
        {
            _currentMode = _availableModes[0];
        }

        RebuildRows();
    }

    [JSInvokable]
    public void OnContainerWidthChanged(int width)
    {
        if (width == _containerWidth) return;
        _containerWidth = width;
        RebuildRows();
        StateHasChanged();
    }

    private async Task SetViewModeAsync(MediaBrowserViewMode mode)
    {
        if (mode == _currentMode) return;
        _currentMode = mode;

        if (mode is MediaBrowserViewMode.Grid)
        {
            StateHasChanged();
            await Task.Yield();
            await StartObservingGridWidth();
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
        if (Items is null || Items.Count == 0 || _currentMode is not MediaBrowserViewMode.Grid)
        {
            _rows = [];
            return;
        }

        var cols = CalculateColumnCount();
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

        await Task.Yield();
        await _module.InvokeVoidAsync("observeContainerWidth", _gridRef, _dotnetRef);
    }

    private async Task SaveSettingsAsync()
    {
        if (_module is null) return;
        var settings = new MediaBrowserSettings
        {
            Mode = _currentMode,
            ItemWidth = _itemWidth,
            Spacing = _spacing
        };
        await _module.InvokeVoidAsync("saveSettings", PersistenceKey, settings);
    }

    private List<MediaBrowserViewMode> BuildAvailableModes()
    {
        var modes = new List<MediaBrowserViewMode>();
        if (GridTemplate is not null) modes.Add(MediaBrowserViewMode.Grid);
        if (TableHeaderContent is not null && TableRowTemplate is not null) modes.Add(MediaBrowserViewMode.Table);
        if (ListTemplate is not null) modes.Add(MediaBrowserViewMode.List);
        return modes;
    }

    private static string GetModeIcon(MediaBrowserViewMode mode) => mode switch
    {
        MediaBrowserViewMode.Grid => Icons.Material.Outlined.GridView,
        MediaBrowserViewMode.Table => Icons.Material.Outlined.TableRows,
        MediaBrowserViewMode.List => Icons.Material.Outlined.ViewList,
        _ => Icons.Material.Outlined.GridView
    };

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                if (_currentMode is MediaBrowserViewMode.Grid)
                {
                    await _module.InvokeVoidAsync("dispose", _gridRef);
                }
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
        _dotnetRef?.Dispose();
    }

    private sealed class MediaBrowserSettings
    {
        public MediaBrowserViewMode Mode { get; set; }
        public int ItemWidth { get; set; }
        public int Spacing { get; set; }
    }
}

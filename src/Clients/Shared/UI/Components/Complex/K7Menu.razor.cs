using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Complex;

public partial class K7Menu : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Icon { get; set; } = "";
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string Size { get; set; } = "";
    [Parameter] public string Color { get; set; } = "";
    [Parameter] public string Variant { get; set; } = "text";
    [Parameter] public string StartIcon { get; set; } = "";
    [Parameter] public string EndIcon { get; set; } = "";
    [Parameter] public string AriaLabel { get; set; } = "Menu";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }

    private bool _open;
    private ElementReference _root;
    private IJSObjectReference? _module;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/menu.js");
        }
    }

    internal void Close()
    {
        _open = false;
        _ = OpenChanged.InvokeAsync(false);
        StateHasChanged();
    }

    private async Task Toggle()
    {
        _open = !_open;
        await OpenChanged.InvokeAsync(_open);
    }

    private async Task OnFocusOut(FocusEventArgs e)
    {
        if (!_open) return;
        await Task.Delay(100);
        if (!_open) return;
        var hasFocus = _module is not null
            && await _module.InvokeAsync<bool>("containsFocus", _root);
        if (!hasFocus)
        {
            _open = false;
            await OpenChanged.InvokeAsync(false);
        }
    }

    protected override void OnParametersSet()
    {
        if (OpenChanged.HasDelegate)
            _open = Open;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}

using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class RatingStars : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired]
    public Guid MediaId { get; set; }

    [Parameter]
    public int? Value { get; set; }

    [Parameter]
    public EventCallback<int?> ValueChanged { get; set; }

    [Parameter]
    public string Size { get; set; } = "sm";

    private bool _canRate;
    private int? _hoveredValue;
    private ElementReference _element;
    private int? _currentValue;
    private int? _lastParameterValue;
    private int? _valueBeforeEdit;
    private DotNetObjectReference<RatingStars>? _dotNetRef;

    private int StarCount => _currentValue.HasValue ? (int)Math.Ceiling(_currentValue.Value / 2.0) : 0;

    private bool IsFilled(int star)
    {
        if (_hoveredValue.HasValue)
            return star <= _hoveredValue.Value;
        return star <= StarCount;
    }

    protected override async Task OnInitializedAsync()
    {
        _currentValue = Value;
        _lastParameterValue = Value;
        _canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _canRate)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("K7.RatingStars.init", _element, _dotNetRef);
        }
    }

    protected override void OnParametersSet()
    {
        if (Value != _lastParameterValue)
        {
            _currentValue = Value;
            _lastParameterValue = Value;
        }
    }

    private void OnPointerOver(int star)
    {
        _hoveredValue = star;
    }

    private void OnPointerOut()
    {
        _hoveredValue = null;
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is not ("ArrowRight" or "ArrowLeft"))
            return;

        var isEditing = await JS.InvokeAsync<bool>("SpatialNav.isElementEditing", _element);
        if (!isEditing)
            return;

        var newStars = e.Key == "ArrowRight"
            ? Math.Min(5, StarCount + 1)
            : Math.Max(0, StarCount - 1);

        _currentValue = newStars > 0 ? newStars * 2 : null;
        await ValueChanged.InvokeAsync(_currentValue);
    }

    [JSInvokable("OnEditStart")]
    public void OnEditStart()
    {
        _valueBeforeEdit = _currentValue;
    }

    [JSInvokable("OnEditCommit")]
    public async Task OnEditCommit()
    {
        var rating = _currentValue.HasValue ? _currentValue.Value : 0;
        try
        {
            await K7ServerService.RateMediaAsync(MediaId, rating);
        }
        catch
        {
            // Silently fail - optimistic UI
        }
    }

    [JSInvokable("OnEditCancel")]
    public async Task OnEditCancel()
    {
        _currentValue = _valueBeforeEdit;
        await ValueChanged.InvokeAsync(_currentValue);
        await InvokeAsync(StateHasChanged);
    }

    private Task OnStarClick(int star)
    {
        _hoveredValue = null;
        var newStars = star == StarCount ? 0 : star;
        return SetRating(newStars);
    }

    private async Task SetRating(int stars)
    {
        var newValue = stars * 2;
        _currentValue = newValue > 0 ? newValue : null;
        await ValueChanged.InvokeAsync(_currentValue);

        try
        {
            await K7ServerService.RateMediaAsync(MediaId, newValue);
        }
        catch
        {
            // Silently fail - optimistic UI
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef is not null)
        {
            try
            {
                await JS.InvokeVoidAsync("K7.RatingStars.dispose", _element);
            }
            catch (JSDisconnectedException)
            {
            }
            _dotNetRef.Dispose();
        }
    }
}

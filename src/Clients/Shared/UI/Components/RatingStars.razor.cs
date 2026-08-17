using System.Net.Http;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

internal sealed record RatingPointerRect(double Left, double Top, double Width, double Height);

public partial class RatingStars : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter]
    public Guid MediaId { get; set; }

    [Parameter]
    public int? Value { get; set; }

    [Parameter]
    public EventCallback<int?> ValueChanged { get; set; }

    [Parameter]
    public string Size { get; set; } = "sm";

    [Parameter]
    public bool DeferPersistence { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public string? AriaLabel { get; set; }

    private bool _canRate;
    private int? _hoveredValue;
    private ElementReference _element;
    private int? _currentValue;
    private int? _lastParameterValue;
    private Guid _lastMediaId;
    private int? _valueBeforeEdit;
    private DotNetObjectReference<RatingStars>? _dotNetRef;
    private bool _syncSubscribed;
    private bool _isDragging;
    private bool _dragMoved;
    private int? _pointerDownValue;
    private RatingPointerRect? _pointerBounds;

    private string DefaultAriaLabel => RatingStarValue.FormatStarsLabel(_currentValue ?? 0);

    private string SizeClass => $"rating-stars--{NormalizedSize}";

    private string NormalizedSize => Size switch
    {
        "xs" or "sm" or "md" or "lg" => Size,
        _ => "sm"
    };

    private K7IconSize IconSize => NormalizedSize switch
    {
        "xs" => K7IconSize.Xs,
        "md" => K7IconSize.Md,
        "lg" => K7IconSize.Lg,
        _ => K7IconSize.Sm
    };

    private int DisplayValue => _hoveredValue ?? _currentValue ?? 0;

    private string StarModifierClass(int star) => RatingStarValue.StarModifierClass(star, DisplayValue);

    protected override void OnInitialized()
    {
        Ratings.Changed += OnRatingSyncChanged;
        _syncSubscribed = true;
    }

    protected override async Task OnInitializedAsync()
    {
        ApplyValueFromSyncOrParameter();
        if (!ReadOnly)
            _canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || ReadOnly || !_canRate)
            return;

        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("K7.RatingStars.init", _element, _dotNetRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            _dotNetRef?.Dispose();
            _dotNetRef = null;
        }
    }

    protected override void OnParametersSet()
    {
        ApplyValueFromSyncOrParameter();
    }

    private void ApplyValueFromSyncOrParameter()
    {
        if (!DeferPersistence && Ratings.TryGet(MediaId, out var cached))
        {
            _currentValue = cached;
        }
        else if (MediaId != _lastMediaId || Value != _lastParameterValue)
        {
            _currentValue = Value;
        }

        _lastMediaId = MediaId;
        _lastParameterValue = Value;
    }

    private void OnRatingSyncChanged(Guid mediaId, int? value)
    {
        if (DeferPersistence || mediaId != MediaId || _currentValue == value)
            return;

        _currentValue = value;
        _ = InvokeAsync(async () =>
        {
            await ValueChanged.InvokeAsync(value);
            StateHasChanged();
        });
    }

    private async Task OnPointerDown(PointerEventArgs e)
    {
        if (e.Button != 0)
            return;

        _isDragging = true;
        _dragMoved = false;
        await EnsurePointerBoundsAsync();
        var value = ValueFromPointer(e.ClientX);
        _pointerDownValue = value;
        _hoveredValue = value;
    }

    private void OnPointerMove(PointerEventArgs e)
    {
        if (_isDragging)
        {
            var value = ValueFromPointer(e.ClientX);
            if (value != _pointerDownValue)
                _dragMoved = true;
            if (_hoveredValue != value)
                _hoveredValue = value;
            return;
        }

        if (e.PointerType is "mouse" or "pen")
            _ = PreviewHoverAsync(e.ClientX);
    }

    private async Task OnPointerUp(PointerEventArgs e)
    {
        if (!_isDragging)
            return;

        var value = _hoveredValue ?? ValueFromPointer(e.ClientX);
        if (_pointerBounds is { Width: > 0 } && !_dragMoved && value == (_currentValue ?? 0))
            value = 0;

        await EndPointerAsync(value);
    }

    private Task OnPointerCancel()
    {
        if (!_isDragging)
            return Task.CompletedTask;

        _hoveredValue = null;
        return EndPointerAsync(_currentValue ?? 0, persist: false);
    }

    private async Task EndPointerAsync(int value, bool persist = true)
    {
        _isDragging = false;
        _dragMoved = false;
        _pointerDownValue = null;
        _hoveredValue = null;
        _pointerBounds = null;
        if (persist)
            await CommitValueAsync(value);
    }

    private void OnPointerLeave()
    {
        if (_isDragging)
            return;
        _hoveredValue = null;
        _pointerBounds = null;
    }

    private async Task PreviewHoverAsync(double clientX)
    {
        await EnsurePointerBoundsAsync();
        var value = ValueFromPointer(clientX);
        if (_hoveredValue != value)
            _hoveredValue = value;
    }

    private async Task EnsurePointerBoundsAsync()
    {
        if (_pointerBounds is { Width: > 0 })
            return;

        try
        {
            _pointerBounds = await JS.InvokeAsync<RatingPointerRect>("K7.getBoundingRect", _element);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }
    }

    private int ValueFromPointer(double clientX)
    {
        if (_pointerBounds is not { Width: > 0 } bounds)
            return _hoveredValue ?? _currentValue ?? 0;

        var ratio = (clientX - bounds.Left) / bounds.Width;
        return RatingStarValue.FromRatio(ratio);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is not ("ArrowRight" or "ArrowLeft"))
            return;

        var isEditing = await JS.InvokeAsync<bool>("SpatialNav.isElementEditing", _element);
        if (!isEditing)
            return;

        var current = _currentValue ?? 0;
        var next = e.Key == "ArrowRight"
            ? Math.Min(RatingStarValue.Max, current + 1)
            : Math.Max(0, current - 1);

        _currentValue = next > 0 ? next : null;
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
        var rating = _currentValue ?? 0;
        if (DeferPersistence)
        {
            await ValueChanged.InvokeAsync(_currentValue);
            return;
        }

        await RateAsync(rating);
    }

    [JSInvokable("OnEditCancel")]
    public async Task OnEditCancel()
    {
        _currentValue = _valueBeforeEdit;
        await ValueChanged.InvokeAsync(_currentValue);
        await InvokeAsync(StateHasChanged);
    }

    private async Task CommitValueAsync(int value)
    {
        var normalized = value > 0 ? value : (int?)null;
        _currentValue = normalized;
        await ValueChanged.InvokeAsync(_currentValue);
        if (!DeferPersistence)
            await RateAsync(value);
    }

    private async Task RateAsync(int value)
    {
        Ratings.Set(MediaId, value > 0 ? value : null);
        try
        {
            if (Connectivity.IsOnline)
            {
                await K7ServerService.RateMediaAsync(MediaId, value);
            }
            else
            {
                await PlaybackJournal.RecordRatingAsync(MediaId, value);
            }
        }
        catch (HttpRequestException)
        {
            await PlaybackJournal.RecordRatingAsync(MediaId, value);
        }
        catch
        {
            // Silently fail - optimistic UI
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_syncSubscribed)
        {
            Ratings.Changed -= OnRatingSyncChanged;
            _syncSubscribed = false;
        }

        if (_dotNetRef is not null)
        {
            try
            {
                await JS.InvokeVoidAsync("K7.RatingStars.dispose", _element);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
            {
            }
            _dotNetRef.Dispose();
            _dotNetRef = null;
        }
    }
}

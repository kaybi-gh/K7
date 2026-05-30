using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7DialogHost : IDisposable
{
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    private readonly List<K7DialogEntry> _dialogs = [];

    protected override void OnInitialized()
    {
        if (DialogService is K7DialogService svc)
            svc.OnShow += HandleShow;
    }

    private async Task<IK7DialogReference> HandleShow(K7DialogRequest request)
    {
        var entry = new K7DialogEntry(request, () => InvokeAsync(StateHasChanged), OnDialogClosed);
        _dialogs.Add(entry);
        await InvokeAsync(StateHasChanged);

        // Allow two render cycles so the backdrop element is available
        await Task.Yield();
        await Task.Delay(50);

        try
        {
            if (entry.BackdropRef.Id is not null)
                await SpatialNav.AttachLayerCallbackAsync(entry.BackdropRef, entry.CloseCallback);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // Element not yet rendered
        }
        return entry;
    }

    private async void OnDialogClosed(K7DialogEntry entry)
    {
        try
        {
            await SpatialNav.PopLayerAsync(entry.BackdropRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // Element already removed
        }
    }

    private void OnBackdropClick(K7DialogEntry entry)
    {
        if (entry.Options?.BackdropClick != false)
            entry.Cancel();
    }

    private static string GetPaperClasses(K7DialogEntry entry)
    {
        if (entry.Options?.FullScreen == true)
            return "k7-dialog--fullscreen";

        var size = GetSizeClass(entry.Options?.MaxWidth);
        var full = entry.Options?.FullWidth == true ? "k7-dialog--full" : "";
        return $"{size} {full}";
    }

    private static string GetSizeClass(K7DialogMaxWidth? maxWidth) => maxWidth switch
    {
        K7DialogMaxWidth.ExtraExtraSmall or K7DialogMaxWidth.ExtraSmall => "k7-dialog--xs",
        K7DialogMaxWidth.Small => "k7-dialog--sm",
        K7DialogMaxWidth.Medium => "k7-dialog--md",
        K7DialogMaxWidth.Large => "k7-dialog--lg",
        K7DialogMaxWidth.ExtraLarge => "k7-dialog--xl",
        _ => "k7-dialog--sm"
    };

    public void Dispose()
    {
        if (DialogService is K7DialogService svc)
            svc.OnShow -= HandleShow;
    }
}

internal sealed class K7DialogEntry : IK7DialogInstance, IK7DialogReference, IDisposable
{
    private readonly TaskCompletionSource<K7DialogResult> _tcs = new();
    private readonly Func<Task> _stateChanged;
    private readonly Action<K7DialogEntry> _onClosed;
    private readonly LayerCloseCallback _closeCallbackWrapper;

    public Guid Id { get; } = Guid.NewGuid();
    public Type Type { get; }
    public string Title { get; private set; }
    public K7DialogOptions? Options { get; }
    public RenderFragment? HeaderActions { get; private set; }
    public Dictionary<string, object> ComponentParameters { get; }
    public ElementReference BackdropRef { get; set; }
    public DotNetObjectReference<LayerCloseCallback> CloseCallback { get; }

    public Task<K7DialogResult> Result => _tcs.Task;

    public K7DialogEntry(K7DialogRequest request, Func<Task> stateChanged, Action<K7DialogEntry> onClosed)
    {
        Type = request.Type;
        Title = request.Title;
        Options = request.Options;
        _stateChanged = stateChanged;
        _onClosed = onClosed;
        _closeCallbackWrapper = new LayerCloseCallback(Cancel);
        CloseCallback = DotNetObjectReference.Create(_closeCallbackWrapper);

        ComponentParameters = request.Parameters?.ToDictionary()
            .Where(kv => kv.Value is not null)
            .ToDictionary(k => k.Key, v => v.Value!)
            ?? [];
    }

    void IK7DialogInstance.SetTitle(string title) { Title = title; }
    void IK7DialogInstance.SetHeaderActions(RenderFragment? actions) { HeaderActions = actions; _ = _stateChanged(); }
    void IK7DialogInstance.Close() => CloseWith(K7DialogResult.Ok());
    void IK7DialogInstance.Close(K7DialogResult result) => CloseWith(result);
    void IK7DialogInstance.Cancel() => Cancel();

    void IK7DialogReference.Close(K7DialogResult result) => CloseWith(result);
    void IK7DialogReference.Cancel() => Cancel();

    public void Cancel() => CloseWith(K7DialogResult.Cancel());

    private void CloseWith(K7DialogResult result)
    {
        if (_tcs.Task.IsCompleted) return;
        _tcs.TrySetResult(result);
        _onClosed(this);
        _ = _stateChanged();
    }

    public void Dispose()
    {
        CloseCallback.Dispose();
    }
}

public sealed class LayerCloseCallback(Action closeAction)
{
    [JSInvokable]
    public void OnLayerClosed()
    {
        closeAction();
    }
}

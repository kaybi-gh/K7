using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

public partial class HeroSection : IDisposable
{
    private int _activeIndex;
    private bool _paused;
    private Timer? _timer;

    [Parameter] public bool Skeleton { get; set; }
    [Parameter] public bool FullBleed { get; set; }
    [Parameter] public IReadOnlyList<MediaCardViewModel> Items { get; set; } = [];
    [Parameter] public Func<MediaCardViewModel, string>? HrefProvider { get; set; }
    [Parameter] public EventCallback<int> OnActiveIndexChanged { get; set; }

    [Inject] private IStringLocalizer<HeroSection> L { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (_activeIndex >= Items.Count)
            _activeIndex = 0;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender || Items.Count <= 1) return;
        _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(7));
    }

    private void Tick()
    {
        if (_paused) return;
        _ = InvokeAsync(async () =>
        {
            _activeIndex = (_activeIndex + 1) % Items.Count;
            await OnActiveIndexChanged.InvokeAsync(_activeIndex);
            StateHasChanged();
        });
    }

    public async Task GoTo(int index)
    {
        _activeIndex = index;
        ResetTimer();
        await OnActiveIndexChanged.InvokeAsync(index);
    }

    public void Pause() => _paused = true;

    public void Resume()
    {
        _paused = false;
        ResetTimer();
    }

    private void ResetTimer()
    {
        _timer?.Change(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(7));
    }

    private string GetHref(MediaCardViewModel item) =>
        HrefProvider?.Invoke(item) ?? "#";

    public void Dispose()
    {
        _timer?.Dispose();
    }
}

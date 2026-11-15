using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// The banner of a home row on desktop, tablet and phone: backdrop, title, details and a link to
/// the page of the title. With several items, dots switch between them. It stays still unless
/// the row asks for auto-advance, which pauses on hover and focus, can be paused with its
/// button, and never runs when the system asks for reduced motion or the page is hidden.
/// The TV home uses <see cref="HomeTvHero"/> instead.
/// </summary>
public partial class HomeHero : IAsyncDisposable
{
    public const string ModulePath = "./_content/K7.Clients.Shared.UI/js/home-hero.js";

    private const int TickMs = 200;

    private int _index;
    private string? _itemsKey;
    private bool _hovered;
    // Keyboard focus only (a key released inside the banner). A click or a hover that leaves the
    // focus behind must not keep the banner still after the pointer is gone.
    private bool _focused;
    private bool _paused;
    private int _timerSeconds;
    private CancellationTokenSource? _timerCts;
    private int _elapsedMs;
    private int _indicatorRun;
    private string? _previousBackdropUrl;
    private readonly HashSet<string> _failedLogos = [];
    private IJSObjectReference? _module;
    private ElementReference _root;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The titles to feature. The banner renders nothing when the list is empty.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<MediaCardViewModel> Items { get; set; } = [];

    /// <summary>
    /// Name of the row. It names the banner for screen readers and is not shown: above a logo of any
    /// size it only added noise.
    /// </summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>
    /// Runs edge to edge and fades into the page, instead of staying inside the page margins
    /// with rounded corners.
    /// </summary>
    [Parameter] public bool Bleed { get; set; }

    /// <summary>Seconds before the banner moves to its next title on its own. Null or zero keeps it still.</summary>
    [Parameter] public int? AutoAdvanceSeconds { get; set; }

    /// <summary>Address of the page of an item.</summary>
    [Parameter, EditorRequired] public Func<MediaCardViewModel, string> HrefFor { get; set; } = _ => "#";

    private MediaCardViewModel? Current =>
        Items.Count == 0 ? null : Items[Math.Clamp(_index, 0, Items.Count - 1)];

    /// <summary>True while something keeps the banner on its title: the pause button, the pointer or the focus.</summary>
    private bool IsHolding => _paused || _hovered || _focused;

    private string RootClass => string.Join(' ', new[]
    {
        "home-hero",
        Bleed ? "home-hero--bleed" : null,
        AutoAdvances && IsHolding ? "home-hero--holding" : null
    }.Where(c => c is not null));

    // The indicator on the active dot runs for the same time as the wait. A dynamic value, so it
    // travels as a custom property.
    private string? IndicatorStyle => AutoAdvances ? $"--home-hero-delay: {AutoAdvanceSeconds}s" : null;

    private bool AutoAdvances => AutoAdvanceSeconds is > 0 && Items.Count > 1;

    protected override void OnParametersSet()
    {
        // Keep the chosen title while the feed refreshes with the same items.
        var key = string.Join(',', Items.Select(item => item.Id));
        if (key != _itemsKey)
        {
            _itemsKey = key;
            _index = 0;
            _previousBackdropUrl = null;
            _elapsedMs = 0;
        }

        var seconds = AutoAdvances ? AutoAdvanceSeconds!.Value : 0;
        if (seconds == _timerSeconds)
            return;

        _timerSeconds = seconds;
        RestartTimer();
    }

    public async ValueTask DisposeAsync()
    {
        StopTimer();
        if (_module is null)
            return;

        try
        {
            await _module.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
        }
    }

    // Picking a title by hand restarts the wait, so the banner does not move right after.
    private void Select(int index)
    {
        Show(index);
        _elapsedMs = 0;
    }

    private void TogglePause() => _paused = !_paused;

    // The previous artwork stays underneath while the new one fades in: no flash between two titles.
    private void Show(int index)
    {
        if (index == _index)
            return;

        _previousBackdropUrl = Current?.BackdropUrl;
        _index = index;
    }

    private void StopTimer()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;
    }

    private void RestartTimer()
    {
        StopTimer();
        _elapsedMs = 0;
        if (_timerSeconds <= 0)
            return;

        _timerCts = new CancellationTokenSource();
        _ = RunTimerAsync(_timerSeconds, _timerCts.Token);
    }

    // Counts only the time the banner is free to move, like the indicator on the active dot, which
    // pauses with the same state. The tick itself renders nothing.
    private async Task RunTimerAsync(int seconds, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(TickMs));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (IsHolding)
                    continue;

                _elapsedMs += TickMs;
                if (_elapsedMs < seconds * 1000)
                    continue;

                _elapsedMs = 0;
                await InvokeAsync(AdvanceAsync);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task AdvanceAsync()
    {
        if (Items.Count < 2)
            return;

        if (await CanMoveAsync())
            Show((_index + 1) % Items.Count);
        else
            _indicatorRun++; // Stay on the title and start its indicator again.

        StateHasChanged();
    }

    // Loaded on first use: a banner nobody reaches with the keyboard and that stays still never fetches it.
    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);

    // The banner takes the focus on behalf of its button (see focusPrimary in the module, which
    // does nothing when the focus is already on something inside the banner).
    private async Task OnBannerFocusedAsync()
    {
        try
        {
            await (await ModuleAsync()).InvokeVoidAsync("focusPrimary", _root);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
        }
    }

    // Hidden page or reduced motion: stay on the current title.
    private async Task<bool> CanMoveAsync()
    {
        try
        {
            return await (await ModuleAsync()).InvokeAsync<bool>("canAutoAdvance");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            return false;
        }
    }

    // The logo stands for the title when the media has one. A logo that fails to load gives the title back.
    private bool ShowsLogo(MediaCardViewModel item) =>
        !string.IsNullOrEmpty(item.LogoUrl) && !_failedLogos.Contains(item.LogoUrl);

    private void OnLogoFailed(MediaCardViewModel item)
    {
        if (item.LogoUrl is not null)
            _failedLogos.Add(item.LogoUrl);
    }

    private string GetHref(MediaCardViewModel item) => HrefFor(item);

    private string FormatRuntime(int minutes) =>
        minutes >= 60
            ? S["DurationHoursMinutes", minutes / 60, minutes % 60]
            : S["DurationMinutes", minutes];
}

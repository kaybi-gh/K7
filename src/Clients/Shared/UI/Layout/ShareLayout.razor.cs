using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Layout;

public partial class ShareLayout
{
    [Inject] private ISharePlaybackScope PlaybackScope { get; set; } = default!;
    [Inject] private ThemeService ThemeService { get; set; } = default!;
    [Inject] private IServerInfoService ServerInfoService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<ShareLayout> Logger { get; set; } = default!;

    protected override void OnInitialized()
    {
        // Before the players below are created: they ask for settings a visitor does not have.
        PlaybackScope.BeginVisitorPage();

        ThemeService.ThemeOnChange += OnThemeChanged;
        ApplyServerAppearanceAsync().FireAndForget(Logger, "Share appearance bootstrap failed");
    }

    /// <summary>
    /// A visitor has no account: the page takes the look the administrator set for the server
    /// (theme, skins). Someone who came from the app keeps the look already applied.
    /// </summary>
    private async Task ApplyServerAppearanceAsync()
    {
        await ThemeBootstrap.InitializeAsync(ThemeService, JS, ServerInfoService);

        if (!ThemeService.IsResolved)
            ThemeService.ApplyServerDefault();
        else
            await ApplyToDocumentAsync();
    }

    private void OnThemeChanged() => InvokeAsync(ApplyToDocumentAsync).FireAndForget(Logger);

    private async Task ApplyToDocumentAsync()
    {
        try
        {
            await ThemeBootstrap.ApplyToDocumentAsync(JS, ThemeService.Css);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }

    public void Dispose()
    {
        ThemeService.ThemeOnChange -= OnThemeChanged;
        PlaybackScope.EndVisitorPage();
        GC.SuppressFinalize(this);
    }
}

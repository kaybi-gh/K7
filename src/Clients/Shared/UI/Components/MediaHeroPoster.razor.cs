using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Poster of a media page hero. It only exists when the "show the poster" appearance setting
/// of media pages is on; otherwise the page shows the artwork alone. Goes first inside
/// <c>.media-hero</c>, whose layout <c>media-hero.css</c> then turns into two columns.
/// </summary>
public partial class MediaHeroPoster
{
    [Inject] private ThemeService ThemeService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter] public string? Src { get; set; }

    /// <summary>Forces the poster on or off instead of following the appearance setting (demos, tests).</summary>
    [Parameter] public bool? Show { get; set; }

    private bool Shown =>
        !string.IsNullOrEmpty(Src)
        && (Show ?? SkinClasses.PagePoster(ThemeService.Settings, ThemeService.DeviceType, MediaPageRoutes.KindOf(Navigation)));

    protected override void OnInitialized() => ThemeService.ThemeOnChange += OnAppearanceChanged;

    public void Dispose() => ThemeService.ThemeOnChange -= OnAppearanceChanged;

    private void OnAppearanceChanged() => InvokeAsync(StateHasChanged).FireAndForget();
}

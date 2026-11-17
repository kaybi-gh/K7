using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Wraps the sections under the hero of a media page (cast, seasons, reviews, similar titles)
/// and tells the carousels inside how to show: as grids when the appearance asks for it
/// (<see cref="Grid"/>), limited to their first lines or not (<see cref="GridRows"/>).
/// The component adds no element of its own, so the layout of the page is untouched.
/// </summary>
public partial class MediaSections
{
    [Inject] private ThemeService ThemeService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Forces the lists to grids or carousels instead of following the appearance setting (demos, tests).</summary>
    [Parameter] public bool? Lists { get; set; }

    /// <summary>With <see cref="Lists"/> forced to grids: the lines shown before "show more". Null shows everything.</summary>
    [Parameter] public int? Rows { get; set; }

    /// <summary>True when the carousels of the sections show as grids.</summary>
    public bool Grid => Lists ?? SkinClasses.PageGrid(ThemeService.Settings, ThemeService.DeviceType, MediaPageRoutes.KindOf(Navigation));

    /// <summary>Lines a grid shows before its "show more" button. Null shows everything.</summary>
    public int? GridRows => !Grid
        ? null
        : Lists is not null ? Rows : SkinClasses.PageGridRows(ThemeService.Settings, ThemeService.DeviceType, MediaPageRoutes.KindOf(Navigation));

    protected override void OnInitialized() => ThemeService.ThemeOnChange += OnAppearanceChanged;

    public void Dispose() => ThemeService.ThemeOnChange -= OnAppearanceChanged;

    private void OnAppearanceChanged() => InvokeAsync(StateHasChanged).FireAndForget();
}

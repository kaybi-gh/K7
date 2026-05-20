using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public enum MediaCardVariant { Poster, Cover, Backdrop }

public partial class MediaCard
{
    [Parameter] public bool Skeleton { get; set; }
    [Parameter] public MediaCardViewModel Model { get; set; } = default!;
    [Parameter] public MediaCardVariant Variant { get; set; } = MediaCardVariant.Poster;
    [Parameter] public string? Href { get; set; }
    [Parameter] public bool OverlayEnabled { get; set; }
    [Parameter] public bool ProgressEnabled { get; set; }
    [Parameter] public bool WatchedStatusEnabled { get; set; }
    [Parameter] public bool FooterVisible { get; set; }
    [Parameter] public bool ExcludeMenuEnabled { get; set; }
    [Parameter] public bool IsAdmin { get; set; }
    [Parameter] public EventCallback OnExcludeForSelf { get; set; }
    [Parameter] public EventCallback OnExcludeForOthers { get; set; }
    [Parameter] public EventCallback OnFocused { get; set; }

    private bool _menuOpen;

    private string VariantClass => Variant switch
    {
        MediaCardVariant.Cover => "media-card--cover",
        MediaCardVariant.Backdrop => "media-card--backdrop",
        _ => "media-card--poster"
    };

    private bool ProgressBarIsHidden() => Model.Progress < 1 || Model.Progress >= 100;

    private void OnContextMenu() => _menuOpen = true;

    private void OnMenuOpenChanged(bool open) => _menuOpen = open;

    private string PlaceholderIcon => Variant switch
    {
        MediaCardVariant.Cover => Phosphor.VinylRecord,
        _ => Phosphor.FilmSlate
    };
}

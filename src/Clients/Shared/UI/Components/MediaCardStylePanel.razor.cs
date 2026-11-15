using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using K7.Shared.Theming;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Editor of a media card style: what cards say (title, subtitle, extra details), where the
/// details go, and the hover and focus effect, for every kind of media or for movies, series or
/// music alone. Sample cards from the library show the result. Used for the style of one account
/// and for the default style of the server. It edits <see cref="Cards"/> in place and tells the
/// host after each change, which decides when to save.
/// </summary>
public partial class MediaCardStylePanel
{
    /// <summary>What the settings on screen apply to. All stands for the common style.</summary>
    private enum CardScope
    {
        All,
        Movies,
        Series,
        Music
    }

    private static readonly CardDetailField[] _detailFields = Enum.GetValues<CardDetailField>();

    private List<MediaCardViewModel> _samples = [];
    private CardScope _scope = CardScope.All;

    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public MediaCardPreferencesDto Cards { get; set; } = new();

    [Parameter] public bool Disabled { get; set; }

    /// <summary>A setting changed. <see cref="Cards"/> already holds the new value.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    // A new context each render: the samples follow edits made in place, whatever the app shows.
    private MediaCardStyleContext PreviewContext => new(Cards);

    private CardMediaGroup? Group => _scope switch
    {
        CardScope.Movies => CardMediaGroup.Movies,
        CardScope.Series => CardMediaGroup.Series,
        CardScope.Music => CardMediaGroup.Music,
        _ => null
    };

    private bool ScopeHasOverride => Group is { } group && Cards.HasOverride(group);

    /// <summary>What the picked scope shows: its own style, or the common one it follows.</summary>
    private MediaCardStyleDto Current => Cards.Resolve(Group);

    private string ScopeHint => _scope == CardScope.All
        ? L["ScopeHint_All"]
        : string.Format(L[ScopeHasOverride ? "ScopeHint_Own" : "ScopeHint_Common"], L[$"ScopeName_{_scope}"]);

    private string PlacementHint => Current.DetailsPlacement switch
    {
        CardDetailPlacement.Overlay => L["PlacementHint_Overlay"],
        CardDetailPlacement.Hover => L["PlacementHint_Hover"],
        _ => L["PlacementHint_Below"]
    };

    private IReadOnlyList<ButtonGroupOption<CardScope>> ScopeOptions =>
    [
        new(CardScope.All, L["Scope_All"], Phosphor.SquaresFour),
        new(CardScope.Movies, L["Scope_Movies"], Phosphor.FilmSlate),
        new(CardScope.Series, L["Scope_Series"], Phosphor.Television),
        new(CardScope.Music, L["Scope_Music"], Phosphor.MusicNotes)
    ];

    private IReadOnlyList<ButtonGroupOption<CardDetailPlacement>> PlacementOptions =>
    [
        new(CardDetailPlacement.Below, L["Placement_Below"]),
        new(CardDetailPlacement.Overlay, L["Placement_Overlay"]),
        new(CardDetailPlacement.Hover, L["Placement_Hover"])
    ];

    private IReadOnlyList<ButtonGroupOption<CardHoverStyle>> HoverOptions =>
    [
        new(CardHoverStyle.Outline, L["Hover_Outline"]),
        new(CardHoverStyle.Zoom, L["Hover_Zoom"]),
        new(CardHoverStyle.Lift, L["Hover_Lift"])
    ];

    protected override async Task OnInitializedAsync() => _samples = await LoadSamplesAsync();

    private string GetDetailLabel(CardDetailField detail) => L[$"Detail_{detail}"];

    private bool IsInScope(MediaCardViewModel sample) =>
        Group is null || MediaCardStyleContext.GroupOf(sample) == Group;

    private static void IgnoreActivation()
    {
        // Sample cards only show the style: they open nothing.
    }

    /// <summary>
    /// One movie, one series and one album of the library, so the preview shows real artwork and
    /// real details. A kind the library does not have falls back to a made-up title.
    /// </summary>
    private async Task<List<MediaCardViewModel>> LoadSamplesAsync()
    {
        var samples = new List<MediaCardViewModel>();
        foreach (var (type, kind, key) in new[]
                 {
                     (MediaType.Movie, MediaCardKind.Poster, "Sample_Movie"),
                     (MediaType.Serie, MediaCardKind.Serie, "Sample_Serie"),
                     (MediaType.MusicAlbum, MediaCardKind.Cover, "Sample_Album")
                 })
        {
            samples.Add(await LoadSampleAsync(type) ?? new MediaCardViewModel
            {
                Id = $"sample-{type}",
                Kind = kind,
                MediaType = type,
                Title = L[key],
                AdditionalInformations = type == MediaType.MusicAlbum ? L["Sample_Artist"] : "2021",
                ReleaseYear = 2021,
                RuntimeMinutes = type == MediaType.MusicAlbum ? null : 128,
                Rating = 7.8,
                ContentRating = type == MediaType.MusicAlbum ? null : "PG-13",
                Genres = [L["Sample_Genre"]]
            });
        }

        return samples;
    }

    private async Task<MediaCardViewModel?> LoadSampleAsync(MediaType type)
    {
        try
        {
            var page = await MediaService.GetHomeFeedAsync(new GetHomeFeedQuery
            {
                MediaTypes = [type],
                OrderBy = [MediaOrderingOption.CreatedDesc],
                Detailed = true,
                PageNumber = 1,
                PageSize = 1
            });
            return page?.Items?.FirstOrDefault()?.ToCardViewModel(ApiClient);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The style a change goes to. A kind of media that still follows the common style gets its
    /// own copy on its first change.
    /// </summary>
    private MediaCardStyleDto Editable()
    {
        if (Group is not { } group)
            return Cards.Default;

        if (!Cards.Overrides.TryGetValue(group, out var style))
        {
            style = MediaCardStyles.Copy(Cards.Default);
            Cards.Overrides[group] = style;
        }

        return style;
    }

    private void OnScopeChanged(CardScope scope) => _scope = scope;

    private Task RemoveScopeOverride()
    {
        if (Group is { } group)
            Cards.Overrides.Remove(group);
        return OnChanged.InvokeAsync();
    }

    private Task OnShowTitleChanged(bool show)
    {
        Editable().ShowTitle = show;
        return OnChanged.InvokeAsync();
    }

    private Task OnShowSubtitleChanged(bool show)
    {
        Editable().ShowSubtitle = show;
        return OnChanged.InvokeAsync();
    }

    private Task OnDetailChanged(CardDetailField detail, bool selected)
    {
        var style = Editable();
        style.Details = selected
            ? [.. style.Details.Where(d => d != detail), detail]
            : [.. style.Details.Where(d => d != detail)];
        return OnChanged.InvokeAsync();
    }

    private Task OnPlacementChanged(CardDetailPlacement placement)
    {
        if (!Disabled)
            Editable().DetailsPlacement = placement;
        return OnChanged.InvokeAsync();
    }

    private Task OnHoverChanged(CardHoverStyle hover)
    {
        if (!Disabled)
            Editable().Hover = hover;
        return OnChanged.InvokeAsync();
    }
}

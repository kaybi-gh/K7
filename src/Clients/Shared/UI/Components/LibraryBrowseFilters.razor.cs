using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Pages;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class LibraryBrowseFilters
{
    [Inject] private IStringLocalizer<LibraryGroup> LibraryGroupL { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IServerPreferencesService ServerPreferences { get; set; } = default!;

    [Parameter] public MediaTagsDto? Tags { get; set; }
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }
    [Parameter] public RuleGroupDto Filter { get; set; } = MediaBrowseFilterPresets.Empty;
    [Parameter] public EventCallback<RuleGroupDto> FilterChanged { get; set; }
    [Parameter] public IntelligentSearchRequest? ActiveIntelligentSearch { get; set; }
    [Parameter] public EventCallback<IntelligentSearchRequest?> IntelligentSearchChanged { get; set; }
    [Parameter] public MediaType MediaType { get; set; }
    [Parameter] public bool ShowWatchFilters { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;

    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    private bool _menuOpen;
    private bool _musicIntelligenceAvailable;
    private QuickFilterSubmenu _submenu = QuickFilterSubmenu.None;
    private string? _actorDraft;
    private string? _studioDraft;
    private string? _artistDraft;
    private string _intelligentSearchDraft = string.Empty;

    private IReadOnlySet<string> SelectedGenres => MediaBrowseFilterPresets.GetSelectedGenres(Filter);

    private IReadOnlySet<string> SelectedContentRatings => MediaBrowseFilterPresets.GetSelectedContentRatings(Filter);

    private string? SelectedActor => MediaBrowseFilterPresets.GetSearchFieldValue(Filter, nameof(SmartPlaylistField.ActorName));

    private string? SelectedStudio => MediaBrowseFilterPresets.GetSearchFieldValue(Filter, "Studio");

    private string? SelectedArtist => MediaBrowseFilterPresets.GetSearchFieldValue(Filter, nameof(SmartPlaylistField.ArtistName));

    private bool HasActiveFilters =>
        !MediaBrowseFilterPresets.IsEmpty(Filter) || ActiveIntelligentSearch is not null;

    private bool ShowIntelligentSearchFilters =>
        _musicIntelligenceAvailable
        && MediaType is MediaType.MusicTrack or MediaType.MusicAlbum or MediaType.MusicArtist;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var status = await ServerPreferences.GetMusicIntelligenceStatusAsync();
            _musicIntelligenceAvailable = status.IsAvailable;
        }
        catch
        {
            _musicIntelligenceAvailable = false;
        }
    }

    private bool ShowVideoMetadataQuickFilters =>
        MediaType is MediaType.Movie or MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;

    private bool ShowArtistQuickFilter =>
        MediaType is MediaType.MusicTrack or MediaType.MusicAlbum or MediaType.MusicArtist;

    private bool ShowGenreQuickFilter => GenreTags.Count > 0;

    private bool HasContentRatings => Tags.GetValues(MetadataTagKind.ContentRating) is { Count: > 0 };

    private string ActiveFiltersLabel
    {
        get
        {
            if (MediaBrowseFilterPresets.IsInProgress(Filter))
                return L["WatchFilterInProgress"];
            if (MediaBrowseFilterPresets.IsUnwatched(Filter))
                return L["WatchFilterUnwatched"];

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(SelectedActor))
                parts.Add(SelectedActor);
            if (!string.IsNullOrWhiteSpace(SelectedArtist))
                parts.Add(SelectedArtist);
            if (!string.IsNullOrWhiteSpace(SelectedStudio))
                parts.Add(SelectedStudio);

            var contentRatings = SelectedContentRatings;
            if (contentRatings.Count > 0)
                parts.Add(string.Join(", ", contentRatings.OrderBy(r => r, StringComparer.OrdinalIgnoreCase)));

            var genres = SelectedGenres;
            if (genres.Count > 0)
                parts.Add(string.Join(", ", genres.OrderBy(g => g, StringComparer.OrdinalIgnoreCase)));

            if (parts.Count > 0)
                return string.Join(" · ", parts);

            if (Filter.Items.Count > 0)
                return L["AdvancedFilters"];

            if (ActiveIntelligentSearch is { } intelligentSearch)
            {
                return intelligentSearch.Kind == IntelligentSearchKind.Sonic
                    ? string.Format(L["FilterSonicSearchActive"], intelligentSearch.Query)
                    : string.Format(L["FilterLyricsSearchActive"], intelligentSearch.Query);
            }

            return L["Filters"];
        }
    }

    private IReadOnlyList<MediaTagValueDto> GenreTags => Tags.GetTagValues(MetadataTagKind.Genre);

    private string GetGenreLabel(MediaTagValueDto genre) =>
        string.Format(LibraryGroupL["GenreFilterOption"], genre.DisplayName, genre.MediaCount);

    private string GetGenresMenuLabel()
    {
        var count = SelectedGenres.Count;
        return count == 0
            ? L["FilterGenres"]
            : string.Format(L["FilterGenresSelected"], count);
    }

    private string GetActorMenuLabel()
    {
        var value = SelectedActor;
        return string.IsNullOrWhiteSpace(value)
            ? L["FilterActor"]
            : string.Format(L["FilterActorSelected"], value);
    }

    private string GetStudioMenuLabel()
    {
        var value = SelectedStudio;
        return string.IsNullOrWhiteSpace(value)
            ? L["FilterStudio"]
            : string.Format(L["FilterStudioSelected"], value);
    }

    private string GetArtistMenuLabel()
    {
        var value = SelectedArtist;
        return string.IsNullOrWhiteSpace(value)
            ? L["FilterArtist"]
            : string.Format(L["FilterArtistSelected"], value);
    }

    private string GetContentRatingMenuLabel()
    {
        var count = SelectedContentRatings.Count;
        return count == 0
            ? L["FieldContentRating"]
            : string.Format(L["FilterContentRatingSelected"], count);
    }

    private void OnMenuOpenChanged(bool open)
    {
        _menuOpen = open;
        if (!open)
            _submenu = QuickFilterSubmenu.None;
    }

    private void OpenSubmenu(QuickFilterSubmenu submenu)
    {
        _submenu = submenu;
        if (submenu == QuickFilterSubmenu.Actor)
            _actorDraft = SelectedActor;
        else if (submenu == QuickFilterSubmenu.Studio)
            _studioDraft = SelectedStudio;
        else if (submenu == QuickFilterSubmenu.Artist)
            _artistDraft = SelectedArtist;
        else if (submenu is QuickFilterSubmenu.SonicSearch or QuickFilterSubmenu.LyricsSearch)
            _intelligentSearchDraft = ActiveIntelligentSearch?.Query ?? string.Empty;
    }

    private async Task SetPresetAsync(RuleGroupDto preset)
    {
        var next = MediaBrowseFilterPresets.WithPreset(Filter, preset);
        if (ReferenceEquals(next, Filter) || FiltersEqual(next, Filter))
            return;

        await FilterChanged.InvokeAsync(next);
    }

    private async Task ToggleGenreAsync(string genreName)
    {
        await FilterChanged.InvokeAsync(MediaBrowseFilterPresets.ToggleGenre(Filter, genreName));
    }

    private async Task ToggleContentRatingAsync(string contentRating)
    {
        await FilterChanged.InvokeAsync(MediaBrowseFilterPresets.ToggleContentRating(Filter, contentRating));
    }

    private async Task SetActorAsync(string? value)
    {
        await FilterChanged.InvokeAsync(
            MediaBrowseFilterPresets.SetSearchFieldValue(Filter, nameof(SmartPlaylistField.ActorName), value));
    }

    private async Task SetStudioAsync(string? value)
    {
        await FilterChanged.InvokeAsync(MediaBrowseFilterPresets.SetSearchFieldValue(Filter, "Studio", value));
    }

    private async Task SetArtistAsync(string? value)
    {
        await FilterChanged.InvokeAsync(
            MediaBrowseFilterPresets.SetSearchFieldValue(Filter, nameof(SmartPlaylistField.ArtistName), value));
    }

    private async Task ClearFiltersAsync()
    {
        if (!HasActiveFilters)
            return;

        if (ActiveIntelligentSearch is not null)
            await IntelligentSearchChanged.InvokeAsync(null);

        if (!MediaBrowseFilterPresets.IsEmpty(Filter))
            await FilterChanged.InvokeAsync(MediaBrowseFilterPresets.Empty);
    }

    private async Task ApplyIntelligentSearchAsync(IntelligentSearchKind kind)
    {
        var query = _intelligentSearchDraft.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return;

        await IntelligentSearchChanged.InvokeAsync(new IntelligentSearchRequest(kind, query));
        _menuOpen = false;
        _submenu = QuickFilterSubmenu.None;
    }

    private async Task OpenAdvancedFiltersAsync()
    {
        var parameters = new K7DialogParameters
        {
            { nameof(LibraryBrowseAdvancedFiltersDialog.InitialFilter), Filter },
            { nameof(LibraryBrowseAdvancedFiltersDialog.MediaType), MediaType },
            { nameof(LibraryBrowseAdvancedFiltersDialog.LibraryIds), LibraryIds },
            { nameof(LibraryBrowseAdvancedFiltersDialog.LibraryGroupIds), LibraryGroupIds },
            { nameof(LibraryBrowseAdvancedFiltersDialog.Tags), Tags }
        };

        var dialog = await DialogService.ShowAsync<LibraryBrowseAdvancedFiltersDialog>(
            L["AdvancedFilters"],
            parameters,
            new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Large, FullWidth = true });

        var result = await dialog.Result;
        if (result is { Canceled: false, Data: RuleGroupDto filter })
            await FilterChanged.InvokeAsync(filter);
    }

    private async Task<IReadOnlyList<string>> SearchSuggestionsAsync(
        string field,
        string searchText,
        CancellationToken cancellationToken) =>
        await MediaBrowseTagSearch.SearchAsync(
            MediaService,
            field,
            searchText,
            LibraryIds,
            LibraryGroupIds,
            MediaType,
            cancellationToken);

    private static bool FiltersEqual(RuleGroupDto left, RuleGroupDto right) =>
        left.MatchCondition == right.MatchCondition
        && left.Items.Count == right.Items.Count;

    private enum QuickFilterSubmenu
    {
        None,
        Actor,
        Artist,
        Studio,
        ContentRating,
        Genres,
        SonicSearch,
        LyricsSearch
    }
}

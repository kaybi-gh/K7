using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Pages;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class LibraryBrowseFilters
{
    [Inject] private IStringLocalizer<LibraryGroup> LibraryGroupL { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;

    [Parameter] public IReadOnlyList<MediaGenreDto> Genres { get; set; } = [];
    [Parameter] public MediaBrowseFacetsDto? Facets { get; set; }
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }
    [Parameter] public RuleGroupDto Filter { get; set; } = MediaBrowseFilterPresets.Empty;
    [Parameter] public EventCallback<RuleGroupDto> FilterChanged { get; set; }
    [Parameter] public MediaType MediaType { get; set; }
    [Parameter] public bool ShowWatchFilters { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;

    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    private bool _menuOpen;
    private QuickFilterSubmenu _submenu = QuickFilterSubmenu.None;
    private string? _actorDraft;
    private string? _studioDraft;

    private IReadOnlySet<string> SelectedGenres => MediaBrowseFilterPresets.GetSelectedGenres(Filter);

    private IReadOnlySet<string> SelectedContentRatings => MediaBrowseFilterPresets.GetSelectedContentRatings(Filter);

    private string? SelectedActor => MediaBrowseFilterPresets.GetSearchFieldValue(Filter, nameof(SmartPlaylistField.ActorName));

    private string? SelectedStudio => MediaBrowseFilterPresets.GetSearchFieldValue(Filter, "Studio");

    private bool HasActiveFilters => !MediaBrowseFilterPresets.IsEmpty(Filter);

    private bool ShowMetadataQuickFilters =>
        MediaType is MediaType.Movie or MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;

    private bool HasContentRatings => Facets?.ContentRatings is { Count: > 0 };

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

            return L["Filters"];
        }
    }

    private string GetGenreLabel(MediaGenreDto genre) =>
        string.Format(LibraryGroupL["GenreFilterOption"], genre.Name, genre.MediaCount);

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

    private async Task ClearFiltersAsync()
    {
        if (!HasActiveFilters)
            return;

        await FilterChanged.InvokeAsync(MediaBrowseFilterPresets.Empty);
    }

    private async Task OpenAdvancedFiltersAsync()
    {
        var parameters = new K7DialogParameters
        {
            { nameof(LibraryBrowseAdvancedFiltersDialog.InitialFilter), Filter },
            { nameof(LibraryBrowseAdvancedFiltersDialog.MediaType), MediaType },
            { nameof(LibraryBrowseAdvancedFiltersDialog.LibraryIds), LibraryIds },
            { nameof(LibraryBrowseAdvancedFiltersDialog.LibraryGroupIds), LibraryGroupIds },
            { nameof(LibraryBrowseAdvancedFiltersDialog.Genres), Genres },
            { nameof(LibraryBrowseAdvancedFiltersDialog.Facets), Facets }
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
        CancellationToken cancellationToken)
    {
        var results = await MediaService.GetMediaBrowseFilterSuggestionsAsync(new GetMediaBrowseFilterSuggestionsQuery
        {
            LibraryIds = LibraryIds,
            LibraryGroupIds = LibraryGroupIds,
            MediaTypes = MediaType != default ? [MediaType] : null,
            Field = field,
            SearchText = searchText,
            Limit = 20
        }, cancellationToken);

        return results ?? [];
    }

    private static bool FiltersEqual(RuleGroupDto left, RuleGroupDto right) =>
        left.MatchCondition == right.MatchCondition
        && left.Items.Count == right.Items.Count;

    private enum QuickFilterSubmenu
    {
        None,
        Actor,
        Studio,
        ContentRating,
        Genres
    }
}

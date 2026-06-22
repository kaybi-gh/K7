using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class LibraryBrowseAdvancedFiltersDialog
{
    [Inject] private IStringLocalizer<SmartPlaylistDialog> SpL { get; set; } = default!;
    [Inject] private IStringLocalizer<LibraryBrowseFilters> L { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public RuleGroupDto InitialFilter { get; set; } = MediaBrowseFilterPresets.Empty;
    [Parameter] public MediaType MediaType { get; set; }
    [Parameter] public Guid[]? LibraryIds { get; set; }
    [Parameter] public Guid[]? LibraryGroupIds { get; set; }
    [Parameter] public IReadOnlyList<MediaGenreDto> Genres { get; set; } = [];
    [Parameter] public MediaBrowseFacetsDto? Facets { get; set; }

    private RuleGroupDto _filter = MediaBrowseFilterPresets.Empty;
    private IReadOnlyList<RuleFieldDescriptorDto> _fieldDescriptors = [];

    protected override void OnParametersSet()
    {
        _filter = InitialFilter;
        _fieldDescriptors = LocalizeDescriptors(MediaBrowseFilterFields.GetDescriptors(MediaType));
    }

    private void OnFilterChanged(RuleGroupDto value) => _filter = value;

    private void Cancel() => Dialog.Cancel();

    private void Apply() => Dialog.Close(K7DialogResult.Ok(_filter));

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

    private IReadOnlyList<RuleFieldDescriptorDto> LocalizeDescriptors(IReadOnlyList<RuleFieldDescriptorDto> descriptors) =>
        descriptors.Select(LocalizeDescriptor).ToList();

    private RuleFieldDescriptorDto LocalizeDescriptor(RuleFieldDescriptorDto descriptor)
    {
        var localized = descriptor with
        {
            DisplayName = GetFieldLabel(descriptor.FieldName),
            ValuePlaceholder = GetValuePlaceholder(descriptor.FieldName)
        };

        if (descriptor.ValueType == RuleFieldValueType.Boolean && descriptor.Options is not null)
        {
            return localized with
            {
                Options =
                [
                    new RuleFieldOptionDto { Value = "true", Label = L["BooleanTrue"] },
                    new RuleFieldOptionDto { Value = "false", Label = L["BooleanFalse"] }
                ]
            };
        }

        if (TryGetSelectOptions(descriptor.FieldName, out var options))
        {
            return localized with
            {
                ValueType = RuleFieldValueType.Select,
                Operators = SelectOperators,
                Options = options
            };
        }

        return localized;
    }

    private static readonly IReadOnlyList<RuleOperator> SelectOperators =
    [
        RuleOperator.Equals,
        RuleOperator.NotEquals
    ];

    private bool TryGetSelectOptions(string fieldName, out IReadOnlyList<RuleFieldOptionDto> options)
    {
        options = [];

        if (fieldName is nameof(SmartPlaylistField.Genre) or nameof(RestrictionField.Genre))
        {
            if (Genres.Count == 0)
                return false;

            options = Genres
                .Select(g => new RuleFieldOptionDto { Value = g.Name, Label = g.Name })
                .ToList();
            return true;
        }

        if (fieldName == nameof(RestrictionField.ContentRating))
        {
            if (Facets?.ContentRatings is not { Count: > 0 })
                return false;

            options = Facets.ContentRatings
                .Select(r => new RuleFieldOptionDto { Value = r, Label = r })
                .ToList();
            return true;
        }

        return false;
    }

    private string? GetValuePlaceholder(string fieldName) => fieldName switch
    {
        "Studio" => L["SearchPlaceholderStudio"].Value,
        "Network" => L["SearchPlaceholderNetwork"].Value,
        nameof(SmartPlaylistField.ActorName) => L["SearchPlaceholderActor"].Value,
        _ => null
    };

    private string GetFieldLabel(string fieldName) => fieldName switch
    {
        nameof(SmartPlaylistField.Title) => SpL["FieldTitle"],
        nameof(SmartPlaylistField.Genre) => SpL["FieldGenre"],
        nameof(SmartPlaylistField.Year) => SpL["FieldYear"],
        nameof(SmartPlaylistField.Rating) => SpL["FieldRating"],
        nameof(SmartPlaylistField.PlayCount) => SpL["FieldPlayCount"],
        nameof(SmartPlaylistField.DateAdded) => SpL["FieldDateAdded"],
        nameof(SmartPlaylistField.LastPlayed) => SpL["FieldLastPlayed"],
        nameof(SmartPlaylistField.IsCompleted) => SpL["FieldIsWatched"],
        nameof(SmartPlaylistField.ArtistName) => SpL["FieldArtist"],
        nameof(SmartPlaylistField.AlbumTitle) => SpL["FieldAlbum"],
        nameof(SmartPlaylistField.TrackNumber) => SpL["FieldTrackNumber"],
        nameof(SmartPlaylistField.DiscNumber) => SpL["FieldDiscNumber"],
        nameof(SmartPlaylistField.Bpm) => SpL["FieldBpm"],
        nameof(SmartPlaylistField.Duration) => SpL["FieldDuration"],
        nameof(SmartPlaylistField.OriginalLanguage) => SpL["FieldOriginalLanguage"],
        nameof(SmartPlaylistField.ActorName) => SpL["FieldActor"],
        nameof(RestrictionField.ContentRating) => L["FieldContentRating"],
        "Network" => L["FieldNetwork"],
        "Studio" => L["FieldStudio"],
        _ => fieldName
    };
}

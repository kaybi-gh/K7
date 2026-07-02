using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Rules;
using K7.Shared.Extensions;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Helpers;

public static class RuleFieldLocalization
{
    private static readonly IReadOnlyList<RuleOperator> SelectOperators =
    [
        RuleOperator.Equals,
        RuleOperator.NotEquals
    ];

    public static IReadOnlyList<RuleFieldDescriptorDto> Localize(
        IReadOnlyList<RuleFieldDescriptorDto> descriptors,
        IStringLocalizer fieldLabels,
        IStringLocalizer browseLabels,
        MediaTagsDto? tags = null) =>
        descriptors.Select(d => LocalizeDescriptor(d, fieldLabels, browseLabels, tags)).ToList();

    public static string GetFieldLabel(string fieldName, IStringLocalizer fieldLabels, IStringLocalizer browseLabels) =>
        fieldName switch
        {
            nameof(SmartPlaylistField.Title) => fieldLabels["FieldTitle"],
            nameof(SmartPlaylistField.Genre) or nameof(RestrictionField.Genre) => fieldLabels["FieldGenre"],
            nameof(SmartPlaylistField.Year) or nameof(RestrictionField.ReleaseYear) => fieldLabels["FieldYear"],
            nameof(SmartPlaylistField.Rating) => fieldLabels["FieldRating"],
            nameof(SmartPlaylistField.PlayCount) => fieldLabels["FieldPlayCount"],
            nameof(SmartPlaylistField.DateAdded) => fieldLabels["FieldDateAdded"],
            nameof(SmartPlaylistField.LastPlayed) => fieldLabels["FieldLastPlayed"],
            nameof(SmartPlaylistField.IsCompleted) => fieldLabels["FieldIsWatched"],
            nameof(SmartPlaylistField.ArtistName) => fieldLabels["FieldArtist"],
            nameof(SmartPlaylistField.AlbumTitle) => fieldLabels["FieldAlbum"],
            nameof(SmartPlaylistField.TrackNumber) => fieldLabels["FieldTrackNumber"],
            nameof(SmartPlaylistField.DiscNumber) => fieldLabels["FieldDiscNumber"],
            nameof(SmartPlaylistField.Duration) => fieldLabels["FieldDuration"],
            nameof(SmartPlaylistField.OriginalLanguage) => fieldLabels["FieldOriginalLanguage"],
            nameof(SmartPlaylistField.ActorName) => fieldLabels["FieldActor"],
            nameof(RestrictionField.ContentRating) => browseLabels["FieldContentRating"],
            "Network" => browseLabels["FieldNetwork"],
            "Studio" => browseLabels["FieldStudio"],
            _ => fieldName
        };

    private static RuleFieldDescriptorDto LocalizeDescriptor(
        RuleFieldDescriptorDto descriptor,
        IStringLocalizer fieldLabels,
        IStringLocalizer browseLabels,
        MediaTagsDto? tags)
    {
        var localized = descriptor with
        {
            DisplayName = GetFieldLabel(descriptor.FieldName, fieldLabels, browseLabels),
            ValuePlaceholder = GetValuePlaceholder(descriptor.FieldName, fieldLabels, browseLabels)
        };

        if (descriptor.ValueType == RuleFieldValueType.Boolean && descriptor.Options is not null)
        {
            return localized with
            {
                Options =
                [
                    new RuleFieldOptionDto { Value = "true", Label = browseLabels["BooleanTrue"] },
                    new RuleFieldOptionDto { Value = "false", Label = browseLabels["BooleanFalse"] }
                ]
            };
        }

        if (TryGetSelectOptions(descriptor.FieldName, tags, out var options))
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

    private static bool TryGetSelectOptions(
        string fieldName,
        MediaTagsDto? tags,
        out IReadOnlyList<RuleFieldOptionDto> options)
    {
        options = [];

        if (tags is null)
            return false;

        if (fieldName is nameof(SmartPlaylistField.Genre) or nameof(RestrictionField.Genre))
        {
            var genres = tags.GetTagValues(MetadataTagKind.Genre);
            if (genres.Count == 0)
                return false;

            options = genres
                .Select(g => new RuleFieldOptionDto { Value = g.DisplayName, Label = g.DisplayName })
                .ToList();
            return true;
        }

        if (fieldName == nameof(RestrictionField.ContentRating))
        {
            var contentRatings = tags.GetValues(MetadataTagKind.ContentRating);
            if (contentRatings.Count == 0)
                return false;

            options = contentRatings
                .Select(r => new RuleFieldOptionDto { Value = r, Label = r })
                .ToList();
            return true;
        }

        return false;
    }

    private static string? GetValuePlaceholder(
        string fieldName,
        IStringLocalizer fieldLabels,
        IStringLocalizer browseLabels) =>
        fieldName switch
        {
            "Studio" => browseLabels["SearchPlaceholderStudio"].Value,
            "Network" => browseLabels["SearchPlaceholderNetwork"].Value,
            nameof(SmartPlaylistField.ActorName) => browseLabels["SearchPlaceholderActor"].Value,
            nameof(SmartPlaylistField.Year) or nameof(RestrictionField.ReleaseYear) => fieldLabels["PlaceholderYear"].Value,
            nameof(SmartPlaylistField.Rating) => fieldLabels["PlaceholderRating"].Value,
            nameof(SmartPlaylistField.PlayCount) => fieldLabels["PlaceholderCount"].Value,
            nameof(SmartPlaylistField.DateAdded) or nameof(SmartPlaylistField.LastPlayed) => fieldLabels["PlaceholderDays"].Value,
            nameof(SmartPlaylistField.Duration) => fieldLabels["PlaceholderSeconds"].Value,
            nameof(SmartPlaylistField.TrackNumber) or nameof(SmartPlaylistField.DiscNumber) => fieldLabels["PlaceholderTrackDisc"].Value,
            _ => null
        };
}

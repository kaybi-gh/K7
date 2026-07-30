using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Rules;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Helpers;

public static class RuleFieldLocalization
{
    public static IReadOnlyList<RuleFieldDescriptorDto> Localize(
        IReadOnlyList<RuleFieldDescriptorDto> descriptors,
        IStringLocalizer fieldLabels,
        IStringLocalizer browseLabels,
        MediaTagsDto? tags = null) =>
        descriptors.Select(d => LocalizeDescriptor(d, fieldLabels, browseLabels)).ToList();

    public static string GetFieldLabel(string fieldName, IStringLocalizer fieldLabels, IStringLocalizer browseLabels) =>
        fieldName switch
        {
            nameof(DynamicPlaylistField.Title) => fieldLabels["FieldTitle"],
            nameof(DynamicPlaylistField.Genre) or nameof(RestrictionField.Genre) => fieldLabels["FieldGenre"],
            nameof(DynamicPlaylistField.Year) or nameof(RestrictionField.ReleaseYear) => fieldLabels["FieldYear"],
            nameof(DynamicPlaylistField.Rating) => fieldLabels["FieldRating"],
            nameof(DynamicPlaylistField.PlayCount) => fieldLabels["FieldPlayCount"],
            nameof(DynamicPlaylistField.DateAdded) => fieldLabels["FieldDateAdded"],
            nameof(DynamicPlaylistField.LastPlayed) => fieldLabels["FieldLastPlayed"],
            nameof(DynamicPlaylistField.IsCompleted) => fieldLabels["FieldIsWatched"],
            nameof(DynamicPlaylistField.ArtistName) => fieldLabels["FieldArtist"],
            nameof(DynamicPlaylistField.AlbumTitle) => fieldLabels["FieldAlbum"],
            nameof(DynamicPlaylistField.TrackNumber) => fieldLabels["FieldTrackNumber"],
            nameof(DynamicPlaylistField.DiscNumber) => fieldLabels["FieldDiscNumber"],
            nameof(DynamicPlaylistField.Duration) => fieldLabels["FieldDuration"],
            nameof(DynamicPlaylistField.OriginalLanguage) => fieldLabels["FieldOriginalLanguage"],
            nameof(DynamicPlaylistField.ActorName) => fieldLabels["FieldActor"],
            nameof(RestrictionField.ContentRating) => browseLabels["FieldContentRating"],
            "Network" => browseLabels["FieldNetwork"],
            "Studio" => browseLabels["FieldStudio"],
            _ => fieldName
        };

    private static RuleFieldDescriptorDto LocalizeDescriptor(
        RuleFieldDescriptorDto descriptor,
        IStringLocalizer fieldLabels,
        IStringLocalizer browseLabels)
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

        return localized;
    }

    private static string? GetValuePlaceholder(
        string fieldName,
        IStringLocalizer fieldLabels,
        IStringLocalizer browseLabels) =>
        fieldName switch
        {
            "Studio" => browseLabels["SearchPlaceholderStudio"].Value,
            "Network" => browseLabels["SearchPlaceholderNetwork"].Value,
            nameof(DynamicPlaylistField.ActorName) => browseLabels["SearchPlaceholderActor"].Value,
            nameof(DynamicPlaylistField.ArtistName) => browseLabels["SearchPlaceholderArtist"].Value,
            nameof(DynamicPlaylistField.Title) => browseLabels["SearchPlaceholderTitle"].Value,
            nameof(DynamicPlaylistField.AlbumTitle) => browseLabels["SearchPlaceholderAlbum"].Value,
            nameof(DynamicPlaylistField.Genre) or nameof(RestrictionField.Genre) => browseLabels["SearchPlaceholderGenre"].Value,
            nameof(RestrictionField.ContentRating) => browseLabels["FieldContentRating"].Value,
            nameof(DynamicPlaylistField.Year) or nameof(RestrictionField.ReleaseYear) => fieldLabels["PlaceholderYear"].Value,
            nameof(DynamicPlaylistField.Rating) => fieldLabels["PlaceholderRating"].Value,
            nameof(DynamicPlaylistField.PlayCount) => fieldLabels["PlaceholderCount"].Value,
            nameof(DynamicPlaylistField.DateAdded) or nameof(DynamicPlaylistField.LastPlayed) => fieldLabels["PlaceholderDays"].Value,
            nameof(DynamicPlaylistField.Duration) => fieldLabels["PlaceholderSeconds"].Value,
            nameof(DynamicPlaylistField.TrackNumber) or nameof(DynamicPlaylistField.DiscNumber) => fieldLabels["PlaceholderTrackDisc"].Value,
            _ => null
        };
}

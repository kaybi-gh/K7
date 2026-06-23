using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Clients.Shared.Helpers;

public static class MediaBrowseFilterFields
{
    public static IReadOnlyList<RuleFieldDescriptorDto> GetDescriptors(MediaType mediaType) =>
        mediaType switch
        {
            MediaType.Movie => MovieFields,
            MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode => SerieFields,
            MediaType.MusicTrack => MusicTrackFields,
            MediaType.MusicAlbum => MusicAlbumFields,
            MediaType.MusicArtist => MusicArtistFields,
            _ => CommonFields
        };

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> CommonFields =
    [
        TextField(nameof(SmartPlaylistField.Title)),
        TextField(nameof(SmartPlaylistField.Genre)),
        NumberField(nameof(SmartPlaylistField.Year)),
        NumberField(nameof(SmartPlaylistField.Rating)),
        NumberField(nameof(SmartPlaylistField.PlayCount)),
        DateField(nameof(SmartPlaylistField.DateAdded)),
        DateField(nameof(SmartPlaylistField.LastPlayed)),
        BooleanField(nameof(SmartPlaylistField.IsCompleted))
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> MovieFields =
    [
        .. CommonFields,
        LanguageField(nameof(SmartPlaylistField.OriginalLanguage)),
        SearchField(nameof(SmartPlaylistField.ActorName)),
        TextField(nameof(RestrictionField.ContentRating)),
        SearchField("Studio")
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> SerieFields =
    [
        .. CommonFields,
        SearchField(nameof(SmartPlaylistField.ActorName)),
        TextField(nameof(RestrictionField.ContentRating)),
        SearchField("Network"),
        SearchField("Studio")
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> MusicTrackFields =
    [
        .. CommonFields,
        TextField(nameof(SmartPlaylistField.ArtistName)),
        TextField(nameof(SmartPlaylistField.AlbumTitle)),
        NumberField(nameof(SmartPlaylistField.TrackNumber)),
        NumberField(nameof(SmartPlaylistField.DiscNumber)),
        NumberField(nameof(SmartPlaylistField.Bpm)),
        NumberField(nameof(SmartPlaylistField.Duration))
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> MusicAlbumFields =
    [
        TextField(nameof(SmartPlaylistField.Title)),
        SearchField(nameof(SmartPlaylistField.ArtistName)),
        TextField(nameof(SmartPlaylistField.Genre)),
        NumberField(nameof(SmartPlaylistField.Year)),
        NumberField(nameof(SmartPlaylistField.Rating)),
        NumberField(nameof(SmartPlaylistField.PlayCount)),
        DateField(nameof(SmartPlaylistField.DateAdded))
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> MusicArtistFields =
    [
        TextField(nameof(SmartPlaylistField.Title)),
        SearchField(nameof(SmartPlaylistField.ArtistName)),
        TextField(nameof(SmartPlaylistField.Genre)),
        NumberField(nameof(SmartPlaylistField.PlayCount)),
        DateField(nameof(SmartPlaylistField.DateAdded))
    ];

    private static RuleFieldDescriptorDto TextField(string fieldName) => new()
    {
        FieldName = fieldName,
        DisplayName = fieldName,
        ValueType = RuleFieldValueType.Text,
        Operators =
        [
            RuleOperator.Equals,
            RuleOperator.NotEquals,
            RuleOperator.Contains,
            RuleOperator.NotContains,
            RuleOperator.IsEmpty,
            RuleOperator.IsNotEmpty
        ]
    };

    private static RuleFieldDescriptorDto NumberField(string fieldName) => new()
    {
        FieldName = fieldName,
        DisplayName = fieldName,
        ValueType = RuleFieldValueType.Number,
        Operators =
        [
            RuleOperator.Equals,
            RuleOperator.NotEquals,
            RuleOperator.GreaterThan,
            RuleOperator.LessThan,
            RuleOperator.GreaterThanOrEqual,
            RuleOperator.LessThanOrEqual,
            RuleOperator.IsEmpty,
            RuleOperator.IsNotEmpty
        ]
    };

    private static RuleFieldDescriptorDto DateField(string fieldName) => new()
    {
        FieldName = fieldName,
        DisplayName = fieldName,
        ValueType = RuleFieldValueType.Number,
        Operators = [RuleOperator.InLast, RuleOperator.IsEmpty, RuleOperator.IsNotEmpty]
    };

    private static RuleFieldDescriptorDto BooleanField(string fieldName) => new()
    {
        FieldName = fieldName,
        DisplayName = fieldName,
        ValueType = RuleFieldValueType.Boolean,
        Operators = [RuleOperator.Equals],
        Options =
        [
            new RuleFieldOptionDto { Value = "true", Label = "true" },
            new RuleFieldOptionDto { Value = "false", Label = "false" }
        ]
    };

    private static RuleFieldDescriptorDto LanguageField(string fieldName) => new()
    {
        FieldName = fieldName,
        DisplayName = fieldName,
        ValueType = RuleFieldValueType.Language,
        Operators =
        [
            RuleOperator.Equals,
            RuleOperator.NotEquals,
            RuleOperator.IsEmpty,
            RuleOperator.IsNotEmpty
        ]
    };

    private static RuleFieldDescriptorDto SearchField(string fieldName) => new()
    {
        FieldName = fieldName,
        DisplayName = fieldName,
        ValueType = RuleFieldValueType.Search,
        Operators =
        [
            RuleOperator.Equals,
            RuleOperator.NotEquals,
            RuleOperator.Contains,
            RuleOperator.NotContains,
            RuleOperator.IsEmpty,
            RuleOperator.IsNotEmpty
        ]
    };
}

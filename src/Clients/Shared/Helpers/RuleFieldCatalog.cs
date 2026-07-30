using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Clients.Shared.Helpers;

public static class RuleFieldCatalog
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

    public static IReadOnlyList<RuleFieldDescriptorDto> GetAllDescriptors()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<RuleFieldDescriptorDto>();

        foreach (var field in MovieFields
                     .Concat(SerieFields)
                     .Concat(MusicTrackFields)
                     .Concat(MusicAlbumFields)
                     .Concat(MusicArtistFields)
                     .Append(NumberField(nameof(RestrictionField.ReleaseYear))))
        {
            if (seen.Add(field.FieldName))
                result.Add(field);
        }

        return result;
    }

    public static RuleGroupDto Sanitize(RuleGroupDto filter, IReadOnlySet<string> allowedFields) =>
        new()
        {
            MatchCondition = filter.MatchCondition,
            Items = SanitizeItems(filter.Items, allowedFields)
        };

    private static List<RuleGroupItemDto> SanitizeItems(
        IReadOnlyList<RuleGroupItemDto> items,
        IReadOnlySet<string> allowedFields)
    {
        var result = new List<RuleGroupItemDto>();

        foreach (var item in items)
        {
            switch (item)
            {
                case ConditionRuleItemDto rule when allowedFields.Contains(rule.Field):
                    result.Add(rule);
                    break;
                case NestedGroupItemDto group:
                    var sanitizedItems = SanitizeItems(group.Items, allowedFields);
                    if (sanitizedItems.Count > 0)
                    {
                        result.Add(new NestedGroupItemDto
                        {
                            MatchCondition = group.MatchCondition,
                            Items = sanitizedItems
                        });
                    }

                    break;
            }
        }

        return result;
    }

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> CommonFields =
    [
        SearchField(nameof(DynamicPlaylistField.Title)),
        SearchField(nameof(DynamicPlaylistField.Genre)),
        NumberField(nameof(DynamicPlaylistField.Year)),
        NumberField(nameof(DynamicPlaylistField.Rating)),
        NumberField(nameof(DynamicPlaylistField.PlayCount)),
        DateField(nameof(DynamicPlaylistField.DateAdded)),
        DateField(nameof(DynamicPlaylistField.LastPlayed)),
        BooleanField(nameof(DynamicPlaylistField.IsCompleted))
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> MovieFields =
    [
        .. CommonFields,
        LanguageField(nameof(DynamicPlaylistField.OriginalLanguage)),
        SearchField(nameof(DynamicPlaylistField.ActorName)),
        SearchField(nameof(RestrictionField.ContentRating)),
        SearchField("Studio")
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> SerieFields =
    [
        .. CommonFields,
        SearchField(nameof(DynamicPlaylistField.ActorName)),
        SearchField(nameof(RestrictionField.ContentRating)),
        SearchField("Network"),
        SearchField("Studio")
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> MusicTrackFields =
    [
        .. CommonFields,
        SearchField(nameof(DynamicPlaylistField.ArtistName)),
        SearchField(nameof(DynamicPlaylistField.AlbumTitle)),
        NumberField(nameof(DynamicPlaylistField.TrackNumber)),
        NumberField(nameof(DynamicPlaylistField.DiscNumber)),
        NumberField(nameof(DynamicPlaylistField.Duration))
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> MusicAlbumFields =
    [
        SearchField(nameof(DynamicPlaylistField.Title)),
        SearchField(nameof(DynamicPlaylistField.ArtistName)),
        SearchField(nameof(DynamicPlaylistField.Genre)),
        NumberField(nameof(DynamicPlaylistField.Year)),
        NumberField(nameof(DynamicPlaylistField.Rating)),
        NumberField(nameof(DynamicPlaylistField.PlayCount)),
        DateField(nameof(DynamicPlaylistField.DateAdded))
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptorDto> MusicArtistFields =
    [
        SearchField(nameof(DynamicPlaylistField.Title)),
        SearchField(nameof(DynamicPlaylistField.ArtistName)),
        SearchField(nameof(DynamicPlaylistField.Genre)),
        NumberField(nameof(DynamicPlaylistField.PlayCount)),
        DateField(nameof(DynamicPlaylistField.DateAdded))
    ];

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

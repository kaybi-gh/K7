using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class SmartPlaylistDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid? SmartPlaylistId { get; set; }
    [Parameter] public string? InitialTitle { get; set; }
    [Parameter] public string? InitialDescription { get; set; }
    [Parameter] public MediaType InitialMediaType { get; set; } = MediaType.MusicTrack;
    [Parameter] public RuleGroupDto? InitialRuleFilter { get; set; }
    [Parameter] public int? InitialLimit { get; set; }
    [Parameter] public SmartPlaylistOrderBy InitialOrderBy { get; set; } = SmartPlaylistOrderBy.DateAdded;
    [Parameter] public bool InitialOrderDescending { get; set; } = true;

    private bool _isEdit => SmartPlaylistId.HasValue;

    private string _title = "";
    private string? _description;
    private MediaType _mediaType = MediaType.MusicTrack;
    private SmartPlaylistMatchCondition _matchCondition = SmartPlaylistMatchCondition.All;
    private List<RuleViewModel> _rules = [];
    private int? _limit;
    private SmartPlaylistOrderBy _orderBy = SmartPlaylistOrderBy.DateAdded;
    private bool _orderDescending = true;
    private bool _isSubmitting;

    protected override void OnInitialized()
    {
        _title = InitialTitle ?? "";
        _description = InitialDescription;
        _mediaType = InitialMediaType;
        _limit = InitialLimit;
        _orderBy = InitialOrderBy;
        _orderDescending = InitialOrderDescending;

        if (InitialRuleFilter is not null)
        {
            _matchCondition = InitialRuleFilter.MatchCondition == RuleMatchCondition.All
                ? SmartPlaylistMatchCondition.All
                : SmartPlaylistMatchCondition.Any;
            _rules = InitialRuleFilter.Items.OfType<ConditionRuleItemDto>().Select(r => new RuleViewModel
            {
                Field = Enum.TryParse<SmartPlaylistField>(r.Field, out var f) ? f : SmartPlaylistField.Title,
                Operator = MapToLegacyOperator(r.Operator),
                Value = r.Value
            }).ToList();
        }
    }

    private void AddRule()
    {
        _rules.Add(new RuleViewModel { Field = SmartPlaylistField.Title, Operator = SmartPlaylistOperator.Contains });
    }

    private void RemoveRule(int index)
    {
        if (index >= 0 && index < _rules.Count)
            _rules.RemoveAt(index);
    }

    private void OnFieldChanged(int index, SmartPlaylistField field)
    {
        _rules[index].Field = field;
        var operators = GetAvailableOperators(field);
        if (!operators.Contains(_rules[index].Operator))
            _rules[index].Operator = operators.First();
        _rules[index].Value = null;
    }

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_title)) return;
        _isSubmitting = true;
        try
        {
            var ruleFilter = BuildRuleGroupDto();

            if (_isEdit)
            {
                await K7ServerService.UpdateSmartPlaylistAsync(SmartPlaylistId!.Value, new UpdateSmartPlaylistRequest
                {
                    Title = _title.Trim(),
                    Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                    MediaType = _mediaType,
                    RuleFilter = ruleFilter,
                    Limit = _limit,
                    OrderBy = _orderBy,
                    OrderDescending = _orderDescending
                });
                Snackbar.Add(L["Updated"], K7Severity.Success);
                Dialog.Close(K7DialogResult.Ok(SmartPlaylistId!.Value));
            }
            else
            {
                var id = await K7ServerService.CreateSmartPlaylistAsync(new CreateSmartPlaylistRequest
                {
                    Title = _title.Trim(),
                    Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                    MediaType = _mediaType,
                    RuleFilter = ruleFilter,
                    Limit = _limit,
                    OrderBy = _orderBy,
                    OrderDescending = _orderDescending
                });
                Snackbar.Add(L["Created"], K7Severity.Success);
                Dialog.Close(K7DialogResult.Ok(id));
            }
        }
        catch { Snackbar.Add(L["SaveError"], K7Severity.Error); }
        finally { _isSubmitting = false; }
    }

    private static bool IsUnaryOperator(SmartPlaylistOperator op) =>
        op is SmartPlaylistOperator.IsEmpty or SmartPlaylistOperator.IsNotEmpty;

    private SmartPlaylistField[] GetAvailableFields() =>
    [
        SmartPlaylistField.Title,
        SmartPlaylistField.Genre,
        SmartPlaylistField.Year,
        SmartPlaylistField.Rating,
        SmartPlaylistField.PlayCount,
        SmartPlaylistField.DateAdded,
        SmartPlaylistField.LastPlayed,
        SmartPlaylistField.IsCompleted,
        SmartPlaylistField.ArtistName,
        SmartPlaylistField.AlbumTitle,
        SmartPlaylistField.TrackNumber,
        SmartPlaylistField.DiscNumber,
        SmartPlaylistField.Bpm,
        SmartPlaylistField.Duration,
        SmartPlaylistField.OriginalLanguage,
        SmartPlaylistField.ActorName
    ];

    private static SmartPlaylistOperator[] GetAvailableOperators(SmartPlaylistField field) => field switch
    {
        SmartPlaylistField.Title or SmartPlaylistField.ArtistName or SmartPlaylistField.AlbumTitle
            or SmartPlaylistField.ActorName =>
            [SmartPlaylistOperator.Equals, SmartPlaylistOperator.NotEquals, SmartPlaylistOperator.Contains,
             SmartPlaylistOperator.IsEmpty, SmartPlaylistOperator.IsNotEmpty],

        SmartPlaylistField.OriginalLanguage =>
            [SmartPlaylistOperator.Equals, SmartPlaylistOperator.NotEquals,
             SmartPlaylistOperator.IsEmpty, SmartPlaylistOperator.IsNotEmpty],

        SmartPlaylistField.Genre =>
            [SmartPlaylistOperator.Equals, SmartPlaylistOperator.NotEquals, SmartPlaylistOperator.Contains,
             SmartPlaylistOperator.IsEmpty, SmartPlaylistOperator.IsNotEmpty],

        SmartPlaylistField.Year or SmartPlaylistField.TrackNumber or SmartPlaylistField.DiscNumber
            or SmartPlaylistField.PlayCount =>
            [SmartPlaylistOperator.Equals, SmartPlaylistOperator.NotEquals,
             SmartPlaylistOperator.GreaterThan, SmartPlaylistOperator.LessThan,
             SmartPlaylistOperator.GreaterThanOrEqual, SmartPlaylistOperator.LessThanOrEqual],

        SmartPlaylistField.Rating or SmartPlaylistField.Bpm or SmartPlaylistField.Duration =>
            [SmartPlaylistOperator.Equals, SmartPlaylistOperator.GreaterThan, SmartPlaylistOperator.LessThan,
             SmartPlaylistOperator.GreaterThanOrEqual, SmartPlaylistOperator.LessThanOrEqual,
             SmartPlaylistOperator.IsEmpty, SmartPlaylistOperator.IsNotEmpty],

        SmartPlaylistField.DateAdded or SmartPlaylistField.LastPlayed =>
            [SmartPlaylistOperator.InLast, SmartPlaylistOperator.IsEmpty, SmartPlaylistOperator.IsNotEmpty],

        SmartPlaylistField.IsCompleted =>
            [SmartPlaylistOperator.Equals],

        _ => [SmartPlaylistOperator.Equals, SmartPlaylistOperator.NotEquals, SmartPlaylistOperator.Contains]
    };

    private string GetFieldLabel(SmartPlaylistField field) => field switch
    {
        SmartPlaylistField.Title => L["FieldTitle"],
        SmartPlaylistField.Genre => L["FieldGenre"],
        SmartPlaylistField.Year => L["FieldYear"],
        SmartPlaylistField.Rating => L["FieldRating"],
        SmartPlaylistField.PlayCount => L["FieldPlayCount"],
        SmartPlaylistField.DateAdded => L["FieldDateAdded"],
        SmartPlaylistField.LastPlayed => L["FieldLastPlayed"],
        SmartPlaylistField.IsCompleted => L["FieldIsWatched"],
        SmartPlaylistField.ArtistName => L["FieldArtist"],
        SmartPlaylistField.AlbumTitle => L["FieldAlbum"],
        SmartPlaylistField.TrackNumber => L["FieldTrackNumber"],
        SmartPlaylistField.DiscNumber => L["FieldDiscNumber"],
        SmartPlaylistField.Bpm => L["FieldBpm"],
        SmartPlaylistField.Duration => L["FieldDuration"],
        SmartPlaylistField.OriginalLanguage => L["FieldOriginalLanguage"],
        SmartPlaylistField.ActorName => L["FieldActor"],
        _ => field.ToString()
    };

    private string GetOperatorLabel(SmartPlaylistOperator op) => op switch
    {
        SmartPlaylistOperator.Equals => L["OperatorIs"],
        SmartPlaylistOperator.NotEquals => L["OperatorIsNot"],
        SmartPlaylistOperator.Contains => L["OperatorContains"],
        SmartPlaylistOperator.GreaterThan => ">",
        SmartPlaylistOperator.LessThan => "<",
        SmartPlaylistOperator.GreaterThanOrEqual => "≥",
        SmartPlaylistOperator.LessThanOrEqual => "≤",
        SmartPlaylistOperator.InLast => L["OperatorInLastDays"],
        SmartPlaylistOperator.IsEmpty => L["OperatorIsEmpty"],
        SmartPlaylistOperator.IsNotEmpty => L["OperatorIsNotEmpty"],
        _ => op.ToString()
    };

    private string GetValuePlaceholder(SmartPlaylistField field) => field switch
    {
        SmartPlaylistField.Year => L["PlaceholderYear"],
        SmartPlaylistField.Rating => L["PlaceholderRating"],
        SmartPlaylistField.PlayCount => L["PlaceholderCount"],
        SmartPlaylistField.DateAdded or SmartPlaylistField.LastPlayed => L["PlaceholderDays"],
        SmartPlaylistField.IsCompleted => L["PlaceholderBoolean"],
        SmartPlaylistField.Bpm => L["PlaceholderDuration"],
        SmartPlaylistField.Duration => L["PlaceholderSeconds"],
        SmartPlaylistField.TrackNumber or SmartPlaylistField.DiscNumber => L["PlaceholderTrackDisc"],
        _ => L["PlaceholderDefault"]
    };

    private static SmartPlaylistOrderBy[] GetAvailableOrderBy() =>
    [
        SmartPlaylistOrderBy.DateAdded,
        SmartPlaylistOrderBy.Title,
        SmartPlaylistOrderBy.Year,
        SmartPlaylistOrderBy.PlayCount,
        SmartPlaylistOrderBy.Rating,
        SmartPlaylistOrderBy.LastPlayed,
        SmartPlaylistOrderBy.Random,
        SmartPlaylistOrderBy.ArtistName,
        SmartPlaylistOrderBy.AlbumTitle,
        SmartPlaylistOrderBy.TrackNumber,
        SmartPlaylistOrderBy.Bpm,
        SmartPlaylistOrderBy.Duration
    ];

    private string GetOrderByLabel(SmartPlaylistOrderBy order) => order switch
    {
        SmartPlaylistOrderBy.Title => L["OrderByTitle"],
        SmartPlaylistOrderBy.DateAdded => L["OrderByDateAdded"],
        SmartPlaylistOrderBy.LastPlayed => L["OrderByLastPlayed"],
        SmartPlaylistOrderBy.PlayCount => L["OrderByPlayCount"],
        SmartPlaylistOrderBy.Rating => L["OrderByRating"],
        SmartPlaylistOrderBy.Year => L["OrderByYear"],
        SmartPlaylistOrderBy.Random => L["OrderByRandom"],
        SmartPlaylistOrderBy.ArtistName => L["OrderByArtist"],
        SmartPlaylistOrderBy.AlbumTitle => L["OrderByAlbum"],
        SmartPlaylistOrderBy.TrackNumber => L["OrderByTrackNumber"],
        SmartPlaylistOrderBy.Bpm => L["OrderByBpm"],
        SmartPlaylistOrderBy.Duration => L["OrderByDuration"],
        _ => order.ToString()
    };

    internal sealed class RuleViewModel
    {
        public SmartPlaylistField Field { get; set; }
        public SmartPlaylistOperator Operator { get; set; }
        public string? Value { get; set; }
    }

    private RuleGroupDto BuildRuleGroupDto() => new()
    {
        MatchCondition = _matchCondition == SmartPlaylistMatchCondition.All
            ? RuleMatchCondition.All : RuleMatchCondition.Any,
        Items = _rules.Select<RuleViewModel, RuleGroupItemDto>(r => new ConditionRuleItemDto
        {
            Field = r.Field.ToString(),
            Operator = MapToRuleOperator(r.Operator),
            Value = r.Value
        }).ToList()
    };

    private static RuleOperator MapToRuleOperator(SmartPlaylistOperator op) => op switch
    {
        SmartPlaylistOperator.Equals => RuleOperator.Equals,
        SmartPlaylistOperator.NotEquals => RuleOperator.NotEquals,
        SmartPlaylistOperator.Contains => RuleOperator.Contains,
        SmartPlaylistOperator.GreaterThan => RuleOperator.GreaterThan,
        SmartPlaylistOperator.LessThan => RuleOperator.LessThan,
        SmartPlaylistOperator.GreaterThanOrEqual => RuleOperator.GreaterThanOrEqual,
        SmartPlaylistOperator.LessThanOrEqual => RuleOperator.LessThanOrEqual,
        SmartPlaylistOperator.InLast => RuleOperator.InLast,
        SmartPlaylistOperator.IsEmpty => RuleOperator.IsEmpty,
        SmartPlaylistOperator.IsNotEmpty => RuleOperator.IsNotEmpty,
        _ => RuleOperator.Equals
    };

    private static SmartPlaylistOperator MapToLegacyOperator(RuleOperator op) => op switch
    {
        RuleOperator.Equals => SmartPlaylistOperator.Equals,
        RuleOperator.NotEquals => SmartPlaylistOperator.NotEquals,
        RuleOperator.Contains => SmartPlaylistOperator.Contains,
        RuleOperator.GreaterThan => SmartPlaylistOperator.GreaterThan,
        RuleOperator.LessThan => SmartPlaylistOperator.LessThan,
        RuleOperator.GreaterThanOrEqual => SmartPlaylistOperator.GreaterThanOrEqual,
        RuleOperator.LessThanOrEqual => SmartPlaylistOperator.LessThanOrEqual,
        RuleOperator.InLast => SmartPlaylistOperator.InLast,
        RuleOperator.IsEmpty => SmartPlaylistOperator.IsEmpty,
        RuleOperator.IsNotEmpty => SmartPlaylistOperator.IsNotEmpty,
        _ => SmartPlaylistOperator.Equals
    };
}

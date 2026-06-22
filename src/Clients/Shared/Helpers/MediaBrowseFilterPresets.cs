using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Clients.Shared.Helpers;

public static class MediaBrowseFilterPresets
{
    public static RuleGroupDto Empty { get; } = new()
    {
        MatchCondition = RuleMatchCondition.All,
        Items = []
    };

    public static RuleGroupDto Unwatched { get; } = new()
    {
        MatchCondition = RuleMatchCondition.All,
        Items =
        [
            new ConditionRuleItemDto
            {
                Field = nameof(SmartPlaylistField.IsCompleted),
                Operator = RuleOperator.Equals,
                Value = "false"
            }
        ]
    };

    public static RuleGroupDto InProgress { get; } = new()
    {
        MatchCondition = RuleMatchCondition.All,
        Items =
        [
            new ConditionRuleItemDto
            {
                Field = "InProgress",
                Operator = RuleOperator.Equals,
                Value = "true"
            }
        ]
    };

    public static bool IsEmpty(RuleGroupDto filter) =>
        filter.Items.Count == 0;

    public static bool IsUnwatched(RuleGroupDto filter) =>
        filter.MatchCondition == RuleMatchCondition.All
        && filter.Items.Count == 1
        && filter.Items[0] is ConditionRuleItemDto rule
        && rule.Field == nameof(SmartPlaylistField.IsCompleted)
        && rule.Operator == RuleOperator.Equals
        && rule.Value == "false";

    public static bool IsInProgress(RuleGroupDto filter) =>
        filter.MatchCondition == RuleMatchCondition.All
        && filter.Items.Count == 1
        && filter.Items[0] is ConditionRuleItemDto rule
        && rule.Field == "InProgress";

    public static IReadOnlySet<string> GetSelectedGenres(RuleGroupDto filter) =>
        GetMultiSelectValues(filter, nameof(SmartPlaylistField.Genre));

    public static IReadOnlySet<string> GetSelectedContentRatings(RuleGroupDto filter) =>
        GetMultiSelectValues(filter, nameof(RestrictionField.ContentRating));

    public static string? GetSearchFieldValue(RuleGroupDto filter, string fieldName) =>
        filter.Items
            .OfType<ConditionRuleItemDto>()
            .FirstOrDefault(r => r.Field == fieldName && IsSearchOperator(r.Operator))
            ?.Value;

    public static RuleGroupDto SetSearchFieldValue(RuleGroupDto filter, string fieldName, string? value)
    {
        var otherRules = filter.Items
            .Where(i => i is not ConditionRuleItemDto rule || rule.Field != fieldName)
            .ToList();

        if (string.IsNullOrWhiteSpace(value))
        {
            return new RuleGroupDto
            {
                MatchCondition = filter.MatchCondition,
                Items = otherRules
            };
        }

        otherRules.Add(new ConditionRuleItemDto
        {
            Field = fieldName,
            Operator = RuleOperator.Contains,
            Value = value.Trim()
        });

        return new RuleGroupDto
        {
            MatchCondition = filter.MatchCondition,
            Items = otherRules
        };
    }

    public static RuleGroupDto ToggleGenre(RuleGroupDto filter, string genreName)
    {
        var genreRules = filter.Items.OfType<ConditionRuleItemDto>()
            .Where(r => r.Field == nameof(SmartPlaylistField.Genre) && r.Operator == RuleOperator.Equals)
            .ToList();
        var otherRules = filter.Items.Where(i => i is not ConditionRuleItemDto rule
            || rule.Field != nameof(SmartPlaylistField.Genre)
            || rule.Operator != RuleOperator.Equals).ToList();

        if (genreRules.Any(r => string.Equals(r.Value, genreName, StringComparison.OrdinalIgnoreCase)))
            genreRules.RemoveAll(r => string.Equals(r.Value, genreName, StringComparison.OrdinalIgnoreCase));
        else
            genreRules.Add(new ConditionRuleItemDto
            {
                Field = nameof(SmartPlaylistField.Genre),
                Operator = RuleOperator.Equals,
                Value = genreName
            });

        return new RuleGroupDto
        {
            MatchCondition = filter.MatchCondition,
            Items = otherRules.Concat<RuleGroupItemDto>(genreRules).ToList()
        };
    }

    public static RuleGroupDto ToggleContentRating(RuleGroupDto filter, string contentRating) =>
        ToggleMultiSelectValue(filter, nameof(RestrictionField.ContentRating), contentRating);

    public static RuleGroupDto WithPreset(RuleGroupDto current, RuleGroupDto preset)
    {
        var preservedRules = current.Items.Where(i => i is ConditionRuleItemDto rule && IsQuickFilterField(rule.Field)).ToList();

        if (preset.Items.Count == 0)
            return new RuleGroupDto { MatchCondition = RuleMatchCondition.All, Items = preservedRules };

        return new RuleGroupDto
        {
            MatchCondition = RuleMatchCondition.All,
            Items = preset.Items.Concat(preservedRules).ToList()
        };
    }

    private static IReadOnlySet<string> GetMultiSelectValues(RuleGroupDto filter, string fieldName) =>
        filter.Items
            .OfType<ConditionRuleItemDto>()
            .Where(r => r.Field == fieldName && r.Operator == RuleOperator.Equals && r.Value is not null)
            .Select(r => r.Value!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static RuleGroupDto ToggleMultiSelectValue(RuleGroupDto filter, string fieldName, string value)
    {
        var valueRules = filter.Items.OfType<ConditionRuleItemDto>()
            .Where(r => r.Field == fieldName && r.Operator == RuleOperator.Equals)
            .ToList();
        var otherRules = filter.Items.Where(i => i is not ConditionRuleItemDto rule
            || rule.Field != fieldName
            || rule.Operator != RuleOperator.Equals).ToList();

        if (valueRules.Any(r => string.Equals(r.Value, value, StringComparison.OrdinalIgnoreCase)))
            valueRules.RemoveAll(r => string.Equals(r.Value, value, StringComparison.OrdinalIgnoreCase));
        else
            valueRules.Add(new ConditionRuleItemDto
            {
                Field = fieldName,
                Operator = RuleOperator.Equals,
                Value = value
            });

        return new RuleGroupDto
        {
            MatchCondition = filter.MatchCondition,
            Items = otherRules.Concat<RuleGroupItemDto>(valueRules).ToList()
        };
    }

    private static bool IsQuickFilterField(string fieldName) =>
        fieldName is nameof(SmartPlaylistField.Genre)
            or nameof(SmartPlaylistField.ActorName)
            or "Studio"
            or nameof(RestrictionField.ContentRating);

    private static bool IsSearchOperator(RuleOperator ruleOperator) =>
        ruleOperator is RuleOperator.Equals or RuleOperator.Contains;
}

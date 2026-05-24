using System.Linq.Expressions;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Enums;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Application.Features.Restrictions.Services;

public static class ContentRestrictionEvaluator
{
    public static IQueryable<BaseMedia> ApplyRestriction(
        IQueryable<BaseMedia> query,
        ContentRestrictionProfile profile)
    {
        if (profile.RuleFilter.Items.Count == 0)
            return query;

        var restricted = BuildGroupPredicate(profile.RuleFilter);
        return query.Where(Negate(restricted));
    }

    public static IQueryable<BaseMedia> GetRestricted(
        IQueryable<BaseMedia> query,
        ContentRestrictionProfile profile)
    {
        if (profile.RuleFilter.Items.Count == 0)
            return query.Where(_ => false);

        var restricted = BuildGroupPredicate(profile.RuleFilter);
        return query.Where(restricted);
    }

    private static Expression<Func<BaseMedia, bool>> BuildGroupPredicate(RuleGroup group)
    {
        var predicates = new List<Expression<Func<BaseMedia, bool>>>();

        foreach (var item in group.Items)
        {
            switch (item)
            {
                case ConditionRuleItem rule:
                    predicates.Add(BuildRulePredicate(rule));
                    break;
                case NestedGroupItem nested:
                    var nestedGroup = new RuleGroup { MatchCondition = nested.MatchCondition, Items = nested.Items };
                    predicates.Add(BuildGroupPredicate(nestedGroup));
                    break;
            }
        }

        if (predicates.Count == 0)
            return _ => true;

        return group.MatchCondition == RuleMatchCondition.All
            ? predicates.Aggregate(CombineAnd)
            : predicates.Aggregate(CombineOr);
    }

    private static Expression<Func<BaseMedia, bool>> BuildRulePredicate(ConditionRuleItem rule)
    {
        return rule.Field switch
        {
            nameof(RestrictionField.Genre) => BuildGenrePredicate(rule),
            nameof(RestrictionField.ContentRating) => BuildContentRatingPredicate(rule),
            nameof(RestrictionField.ReleaseYear) => BuildReleaseYearPredicate(rule),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildGenrePredicate(ConditionRuleItem rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RuleOperator.Equals => m => m.Genres.Any(g => g == value),
            RuleOperator.NotEquals => m => !m.Genres.Any(g => g == value),
            RuleOperator.Contains => m => m.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")),
            RuleOperator.NotContains => m => !m.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")),
            RuleOperator.IsEmpty => m => !m.Genres.Any(),
            RuleOperator.IsNotEmpty => m => m.Genres.Any(),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildContentRatingPredicate(ConditionRuleItem rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RuleOperator.Equals => m => m is Movie && ((Movie)m).ContentRating == value,
            RuleOperator.NotEquals => m => !(m is Movie) || ((Movie)m).ContentRating != value,
            RuleOperator.Contains => m => m is Movie && ((Movie)m).ContentRating != null && EF.Functions.Like(((Movie)m).ContentRating!, $"%{value}%"),
            RuleOperator.NotContains => m => !(m is Movie) || ((Movie)m).ContentRating == null || !EF.Functions.Like(((Movie)m).ContentRating!, $"%{value}%"),
            RuleOperator.IsEmpty => m => !(m is Movie) || ((Movie)m).ContentRating == null || ((Movie)m).ContentRating == "",
            RuleOperator.IsNotEmpty => m => m is Movie && ((Movie)m).ContentRating != null && ((Movie)m).ContentRating != "",
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildReleaseYearPredicate(ConditionRuleItem rule)
    {
        if (rule.Operator == RuleOperator.IsEmpty)
            return m => m.ReleaseDate == null;
        if (rule.Operator == RuleOperator.IsNotEmpty)
            return m => m.ReleaseDate != null;

        if (!int.TryParse(rule.Value, out var year)) return _ => true;
        return rule.Operator switch
        {
            RuleOperator.Equals => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year == year,
            RuleOperator.NotEquals => m => m.ReleaseDate == null || m.ReleaseDate.Value.Year != year,
            RuleOperator.GreaterThan => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year > year,
            RuleOperator.LessThan => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year < year,
            RuleOperator.GreaterThanOrEqual => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year >= year,
            RuleOperator.LessThanOrEqual => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year <= year,
            _ => _ => true
        };
    }

    private static Expression<Func<T, bool>> Negate<T>(Expression<Func<T, bool>> predicate)
    {
        var param = predicate.Parameters[0];
        var body = Expression.Not(predicate.Body);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    private static Expression<Func<T, bool>> CombineOr<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var param = Expression.Parameter(typeof(T));
        var body = Expression.OrElse(
            Expression.Invoke(left, param),
            Expression.Invoke(right, param));
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    private static Expression<Func<T, bool>> CombineAnd<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var param = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(left, param),
            Expression.Invoke(right, param));
        return Expression.Lambda<Func<T, bool>>(body, param);
    }
}

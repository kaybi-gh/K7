using System.Linq.Expressions;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Restrictions.Services;

public static class ContentRestrictionEvaluator
{
    public static IQueryable<BaseMedia> ApplyRestriction(
        IQueryable<BaseMedia> query,
        ContentRestrictionProfile profile)
    {
        if (profile.Rules.Count == 0)
            return query;

        var predicates = profile.Rules
            .Select(BuildPredicate)
            .ToList();

        Expression<Func<BaseMedia, bool>> restricted;
        if (profile.MatchCondition == RestrictionMatchCondition.All)
        {
            restricted = predicates.Aggregate(CombineAnd);
        }
        else
        {
            restricted = predicates.Aggregate(CombineOr);
        }

        return query.Where(Negate(restricted));
    }

    public static IQueryable<BaseMedia> GetRestricted(
        IQueryable<BaseMedia> query,
        ContentRestrictionProfile profile)
    {
        if (profile.Rules.Count == 0)
            return query.Where(_ => false);

        var predicates = profile.Rules
            .Select(BuildPredicate)
            .ToList();

        Expression<Func<BaseMedia, bool>> restricted;
        if (profile.MatchCondition == RestrictionMatchCondition.All)
        {
            restricted = predicates.Aggregate(CombineAnd);
        }
        else
        {
            restricted = predicates.Aggregate(CombineOr);
        }

        return query.Where(restricted);
    }

    private static Expression<Func<BaseMedia, bool>> BuildPredicate(ContentRestrictionRule rule)
    {
        return rule.Field switch
        {
            RestrictionField.Genre => BuildGenrePredicate(rule),
            RestrictionField.ContentRating => BuildContentRatingPredicate(rule),
            RestrictionField.ReleaseYear => BuildReleaseYearPredicate(rule),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildGenrePredicate(ContentRestrictionRule rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RestrictionOperator.Equals => m => m.Genres.Any(g => g == value),
            RestrictionOperator.NotEquals => m => !m.Genres.Any(g => g == value),
            RestrictionOperator.Contains => m => m.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")),
            RestrictionOperator.NotContains => m => !m.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildContentRatingPredicate(ContentRestrictionRule rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RestrictionOperator.Equals => m => m is Movie && ((Movie)m).ContentRating == value,
            RestrictionOperator.NotEquals => m => !(m is Movie) || ((Movie)m).ContentRating != value,
            RestrictionOperator.Contains => m => m is Movie && ((Movie)m).ContentRating != null && EF.Functions.Like(((Movie)m).ContentRating!, $"%{value}%"),
            RestrictionOperator.NotContains => m => !(m is Movie) || ((Movie)m).ContentRating == null || !EF.Functions.Like(((Movie)m).ContentRating!, $"%{value}%"),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildReleaseYearPredicate(ContentRestrictionRule rule)
    {
        if (!int.TryParse(rule.Value, out var year)) return _ => true;
        return rule.Operator switch
        {
            RestrictionOperator.Equals => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year == year,
            RestrictionOperator.NotEquals => m => m.ReleaseDate == null || m.ReleaseDate.Value.Year != year,
            RestrictionOperator.GreaterThan => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year > year,
            RestrictionOperator.LessThan => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year < year,
            RestrictionOperator.GreaterThanOrEqual => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year >= year,
            RestrictionOperator.LessThanOrEqual => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year <= year,
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

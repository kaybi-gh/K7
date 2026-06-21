using System.Linq.Expressions;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Restrictions;
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

        var restricted = MediaRuleEvaluator.BuildGroupPredicate(profile.RuleFilter, userId: null);
        return query.Where(Negate(restricted));
    }

    public static IQueryable<BaseMedia> GetRestricted(
        IQueryable<BaseMedia> query,
        ContentRestrictionProfile profile)
    {
        if (profile.RuleFilter.Items.Count == 0)
            return query.Where(_ => false);

        var restricted = MediaRuleEvaluator.BuildGroupPredicate(profile.RuleFilter, userId: null);
        return query.Where(restricted);
    }

    private static Expression<Func<T, bool>> Negate<T>(Expression<Func<T, bool>> predicate)
    {
        var param = predicate.Parameters[0];
        var body = Expression.Not(predicate.Body);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }
}

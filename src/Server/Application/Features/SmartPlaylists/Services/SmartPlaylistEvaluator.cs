using System.Linq.Expressions;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Application.Features.SmartPlaylists.Services;

public static class SmartPlaylistEvaluator
{
    public static IQueryable<BaseMedia> ApplyRules(
        IQueryable<BaseMedia> query,
        SmartPlaylist smartPlaylist,
        Guid userId)
    {
        query = query.Where(m => m.Type == smartPlaylist.MediaType);

        var filter = smartPlaylist.RuleFilter;
        if (filter.Items.Count == 0)
            return ApplyOrdering(query, smartPlaylist);

        var predicate = BuildGroupPredicate(filter, userId);
        query = query.Where(predicate);

        query = ApplyOrdering(query, smartPlaylist);

        if (smartPlaylist.Limit.HasValue)
            query = query.Take(smartPlaylist.Limit.Value);

        return query;
    }

    private static Expression<Func<BaseMedia, bool>> BuildGroupPredicate(RuleGroup group, Guid userId)
    {
        var predicates = new List<Expression<Func<BaseMedia, bool>>>();

        foreach (var item in group.Items)
        {
            var predicate = item switch
            {
                ConditionRuleItem rule => BuildRulePredicate(rule, userId),
                NestedGroupItem nested => BuildGroupPredicate(
                    new RuleGroup { MatchCondition = nested.MatchCondition, Items = nested.Items }, userId),
                _ => (Expression<Func<BaseMedia, bool>>)(_ => true)
            };
            predicates.Add(predicate);
        }

        if (predicates.Count == 0)
            return _ => true;

        return group.MatchCondition == RuleMatchCondition.All
            ? predicates.Aggregate(CombineAnd)
            : predicates.Aggregate(CombineOr);
    }

    private static Expression<Func<BaseMedia, bool>> BuildRulePredicate(ConditionRuleItem rule, Guid userId)
    {
        return rule.Field switch
        {
            nameof(SmartPlaylistField.Title) => BuildStringPredicate(m => m.Title!, rule),
            nameof(SmartPlaylistField.Genre) => BuildGenrePredicate(rule),
            nameof(SmartPlaylistField.Year) => BuildYearPredicate(rule),
            nameof(SmartPlaylistField.Rating) => BuildRatingPredicate(rule, userId),
            nameof(SmartPlaylistField.PlayCount) => BuildPlayCountPredicate(rule, userId),
            nameof(SmartPlaylistField.DateAdded) => BuildDatePredicate(m => m.Created, rule),
            nameof(SmartPlaylistField.LastPlayed) => BuildLastPlayedPredicate(rule, userId),
            nameof(SmartPlaylistField.IsCompleted) => BuildIsCompletedPredicate(rule, userId),
            nameof(SmartPlaylistField.ArtistName) => BuildArtistNamePredicate(rule),
            nameof(SmartPlaylistField.AlbumTitle) => BuildAlbumTitlePredicate(rule),
            nameof(SmartPlaylistField.TrackNumber) => BuildNullableIntPredicate(m => ((MusicTrack)m).TrackNumber, rule),
            nameof(SmartPlaylistField.DiscNumber) => BuildNullableIntPredicate(m => ((MusicTrack)m).DiscNumber, rule),
            nameof(SmartPlaylistField.Bpm) => BuildNullableDoublePredicate(m => ((MusicTrack)m).AudioAnalysis!.Bpm, rule),
            nameof(SmartPlaylistField.Duration) => BuildDurationPredicate(rule),
            nameof(SmartPlaylistField.OriginalLanguage) => BuildStringPredicate(m => ((Movie)m).OriginalLanguage!, rule),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildStringPredicate(
        Expression<Func<BaseMedia, string>> selector, ConditionRuleItem rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RuleOperator.Equals => Compose(selector, s => s != null && s == value),
            RuleOperator.NotEquals => Compose(selector, s => s == null || s != value),
            RuleOperator.Contains => Compose(selector, s => s != null && EF.Functions.Like(s, $"%{value}%")),
            RuleOperator.NotContains => Compose(selector, s => s == null || !EF.Functions.Like(s, $"%{value}%")),
            RuleOperator.BeginsWith => Compose(selector, s => s != null && EF.Functions.Like(s, $"{value}%")),
            RuleOperator.EndsWith => Compose(selector, s => s != null && EF.Functions.Like(s, $"%{value}")),
            RuleOperator.IsEmpty => Compose(selector, s => s == null || s == ""),
            RuleOperator.IsNotEmpty => Compose(selector, s => s != null && s != ""),
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

    private static Expression<Func<BaseMedia, bool>> BuildYearPredicate(ConditionRuleItem rule)
    {
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

    private static Expression<Func<BaseMedia, bool>> BuildRatingPredicate(ConditionRuleItem rule, Guid userId)
    {
        if (!double.TryParse(rule.Value, System.Globalization.CultureInfo.InvariantCulture, out var rating))
            return _ => true;

        return rule.Operator switch
        {
            RuleOperator.Equals => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value == rating),
            RuleOperator.NotEquals => m => !m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value == rating),
            RuleOperator.GreaterThan => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value > rating),
            RuleOperator.LessThan => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value < rating),
            RuleOperator.GreaterThanOrEqual => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value >= rating),
            RuleOperator.LessThanOrEqual => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value <= rating),
            RuleOperator.IsEmpty => m => !m.Ratings.Any(r => r.Source == RatingSource.LocalUser),
            RuleOperator.IsNotEmpty => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildPlayCountPredicate(ConditionRuleItem rule, Guid userId)
    {
        if (!int.TryParse(rule.Value, out var count)) return _ => true;
        return rule.Operator switch
        {
            RuleOperator.Equals => m => m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount == count),
            RuleOperator.NotEquals => m => !m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount == count),
            RuleOperator.GreaterThan => m => m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount > count),
            RuleOperator.LessThan => m => m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount < count) || !m.UserMediaStates.Any(s => s.UserId == userId),
            RuleOperator.GreaterThanOrEqual => m => m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount >= count),
            RuleOperator.LessThanOrEqual => m => m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount <= count) || !m.UserMediaStates.Any(s => s.UserId == userId),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildDatePredicate(
        Expression<Func<BaseMedia, DateTimeOffset>> selector, ConditionRuleItem rule)
    {
        if (rule.Operator == RuleOperator.InLast && int.TryParse(rule.Value, out var days))
        {
            var threshold = DateTimeOffset.UtcNow.AddDays(-days);
            return Compose(selector, d => d >= threshold);
        }
        return _ => true;
    }

    private static Expression<Func<BaseMedia, bool>> BuildLastPlayedPredicate(ConditionRuleItem rule, Guid userId)
    {
        if (rule.Operator == RuleOperator.InLast && int.TryParse(rule.Value, out var days))
        {
            var threshold = DateTime.UtcNow.AddDays(-days);
            return m => m.UserMediaStates.Any(s => s.UserId == userId && s.LastInteractedAt >= threshold);
        }
        if (rule.Operator == RuleOperator.IsEmpty)
            return m => !m.UserMediaStates.Any(s => s.UserId == userId && s.LastInteractedAt != null);
        if (rule.Operator == RuleOperator.IsNotEmpty)
            return m => m.UserMediaStates.Any(s => s.UserId == userId && s.LastInteractedAt != null);
        return _ => true;
    }

    private static Expression<Func<BaseMedia, bool>> BuildIsCompletedPredicate(ConditionRuleItem rule, Guid userId)
    {
        var isCompleted = rule.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        return rule.Operator switch
        {
            RuleOperator.Equals => isCompleted
                ? m => m.UserMediaStates.Any(s => s.UserId == userId && s.IsCompleted)
                : m => !m.UserMediaStates.Any(s => s.UserId == userId && s.IsCompleted),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildArtistNamePredicate(ConditionRuleItem rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RuleOperator.Equals => m => ((MusicTrack)m).Artist!.Title == value || ((MusicTrack)m).Album!.Artist!.Title == value,
            RuleOperator.NotEquals => m => ((MusicTrack)m).Artist!.Title != value && ((MusicTrack)m).Album!.Artist!.Title != value,
            RuleOperator.Contains => m => EF.Functions.Like(((MusicTrack)m).Artist!.Title!, $"%{value}%") || EF.Functions.Like(((MusicTrack)m).Album!.Artist!.Title!, $"%{value}%"),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildAlbumTitlePredicate(ConditionRuleItem rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RuleOperator.Equals => m => ((MusicTrack)m).Album.Title == value,
            RuleOperator.NotEquals => m => ((MusicTrack)m).Album.Title != value,
            RuleOperator.Contains => m => EF.Functions.Like(((MusicTrack)m).Album.Title!, $"%{value}%"),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildNullableIntPredicate(
        Expression<Func<BaseMedia, int?>> selector, ConditionRuleItem rule)
    {
        if (!int.TryParse(rule.Value, out var val)) return _ => true;
        return rule.Operator switch
        {
            RuleOperator.Equals => Compose(selector, n => n != null && n.Value == val),
            RuleOperator.NotEquals => Compose(selector, n => n == null || n.Value != val),
            RuleOperator.GreaterThan => Compose(selector, n => n != null && n.Value > val),
            RuleOperator.LessThan => Compose(selector, n => n != null && n.Value < val),
            RuleOperator.GreaterThanOrEqual => Compose(selector, n => n != null && n.Value >= val),
            RuleOperator.LessThanOrEqual => Compose(selector, n => n != null && n.Value <= val),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildNullableDoublePredicate(
        Expression<Func<BaseMedia, double?>> selector, ConditionRuleItem rule)
    {
        if (!double.TryParse(rule.Value, System.Globalization.CultureInfo.InvariantCulture, out var val))
            return _ => true;

        return rule.Operator switch
        {
            RuleOperator.Equals => Compose(selector, n => n != null && n.Value == val),
            RuleOperator.NotEquals => Compose(selector, n => n == null || n.Value != val),
            RuleOperator.GreaterThan => Compose(selector, n => n != null && n.Value > val),
            RuleOperator.LessThan => Compose(selector, n => n != null && n.Value < val),
            RuleOperator.GreaterThanOrEqual => Compose(selector, n => n != null && n.Value >= val),
            RuleOperator.LessThanOrEqual => Compose(selector, n => n != null && n.Value <= val),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildDurationPredicate(ConditionRuleItem rule)
    {
        if (!double.TryParse(rule.Value, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return _ => true;

        var duration = TimeSpan.FromSeconds(seconds);
        return rule.Operator switch
        {
            RuleOperator.Equals => m => m.IndexedFiles.Any(f => ((AudioFileMetadata)f.FileMetadata!).Duration == duration),
            RuleOperator.GreaterThan => m => m.IndexedFiles.Any(f => ((AudioFileMetadata)f.FileMetadata!).Duration > duration),
            RuleOperator.LessThan => m => m.IndexedFiles.Any(f => ((AudioFileMetadata)f.FileMetadata!).Duration < duration),
            RuleOperator.GreaterThanOrEqual => m => m.IndexedFiles.Any(f => ((AudioFileMetadata)f.FileMetadata!).Duration >= duration),
            RuleOperator.LessThanOrEqual => m => m.IndexedFiles.Any(f => ((AudioFileMetadata)f.FileMetadata!).Duration <= duration),
            _ => _ => true
        };
    }

    private static IQueryable<BaseMedia> ApplyOrdering(IQueryable<BaseMedia> query, SmartPlaylist sp)
    {
        var desc = sp.OrderDescending;
        return sp.OrderBy switch
        {
            SmartPlaylistOrderBy.Title => desc ? query.OrderByDescending(m => m.Title) : query.OrderBy(m => m.Title),
            SmartPlaylistOrderBy.DateAdded => desc ? query.OrderByDescending(m => m.Created) : query.OrderBy(m => m.Created),
            SmartPlaylistOrderBy.Year => desc ? query.OrderByDescending(m => m.ReleaseDate) : query.OrderBy(m => m.ReleaseDate),
            SmartPlaylistOrderBy.Random => query.OrderBy(_ => EF.Functions.Random()),
            SmartPlaylistOrderBy.ArtistName => desc
                ? query.OrderByDescending(m => ((MusicTrack)m).Artist!.Title ?? ((MusicTrack)m).Album!.Artist!.Title)
                : query.OrderBy(m => ((MusicTrack)m).Artist!.Title ?? ((MusicTrack)m).Album!.Artist!.Title),
            SmartPlaylistOrderBy.AlbumTitle => desc
                ? query.OrderByDescending(m => ((MusicTrack)m).Album.Title)
                : query.OrderBy(m => ((MusicTrack)m).Album.Title),
            SmartPlaylistOrderBy.TrackNumber => desc
                ? query.OrderByDescending(m => ((MusicTrack)m).TrackNumber)
                : query.OrderBy(m => ((MusicTrack)m).TrackNumber),
            SmartPlaylistOrderBy.Bpm => desc
                ? query.OrderByDescending(m => ((MusicTrack)m).AudioAnalysis!.Bpm)
                : query.OrderBy(m => ((MusicTrack)m).AudioAnalysis!.Bpm),
            SmartPlaylistOrderBy.PlayCount => desc
                ? query.OrderByDescending(m => m.UserMediaStates.Sum(s => s.PlayCount))
                : query.OrderBy(m => m.UserMediaStates.Sum(s => s.PlayCount)),
            SmartPlaylistOrderBy.Rating => desc
                ? query.OrderByDescending(m => m.Ratings.Where(r => r.Source == RatingSource.LocalUser).Select(r => r.Value).FirstOrDefault())
                : query.OrderBy(m => m.Ratings.Where(r => r.Source == RatingSource.LocalUser).Select(r => r.Value).FirstOrDefault()),
            SmartPlaylistOrderBy.LastPlayed => desc
                ? query.OrderByDescending(m => m.UserMediaStates.Max(s => s.LastInteractedAt))
                : query.OrderBy(m => m.UserMediaStates.Max(s => s.LastInteractedAt)),
            SmartPlaylistOrderBy.Duration => desc
                ? query.OrderByDescending(m => m.IndexedFiles.Select(f => ((AudioFileMetadata)f.FileMetadata!).Duration).FirstOrDefault())
                : query.OrderBy(m => m.IndexedFiles.Select(f => ((AudioFileMetadata)f.FileMetadata!).Duration).FirstOrDefault()),
            _ => query.OrderByDescending(m => m.Created)
        };
    }

    private static Expression<Func<TSource, bool>> Compose<TSource, TMiddle>(
        Expression<Func<TSource, TMiddle>> selector,
        Expression<Func<TMiddle, bool>> predicate)
    {
        var param = selector.Parameters[0];
        var body = Expression.Invoke(predicate, selector.Body);
        return Expression.Lambda<Func<TSource, bool>>(body, param);
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
}

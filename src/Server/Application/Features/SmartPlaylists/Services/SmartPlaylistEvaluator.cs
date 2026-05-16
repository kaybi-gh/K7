using System.Linq.Expressions;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.SmartPlaylists.Services;

public static class SmartPlaylistEvaluator
{
    public static IQueryable<BaseMedia> ApplyRules(
        IQueryable<BaseMedia> query,
        SmartPlaylist smartPlaylist,
        Guid userId)
    {
        query = query.Where(m => m.Type == smartPlaylist.MediaType);

        if (smartPlaylist.Rules.Count == 0)
            return ApplyOrdering(query, smartPlaylist);

        if (smartPlaylist.MatchCondition == SmartPlaylistMatchCondition.All)
        {
            foreach (var rule in smartPlaylist.Rules)
                query = query.Where(BuildPredicate(rule, userId));
        }
        else
        {
            var predicates = smartPlaylist.Rules
                .Select(r => BuildPredicate(r, userId))
                .ToList();

            var combined = predicates.Aggregate((a, b) => CombineOr(a, b));
            query = query.Where(combined);
        }

        query = ApplyOrdering(query, smartPlaylist);

        if (smartPlaylist.Limit.HasValue)
            query = query.Take(smartPlaylist.Limit.Value);

        return query;
    }

    private static Expression<Func<BaseMedia, bool>> BuildPredicate(SmartPlaylistRule rule, Guid userId)
    {
        return rule.Field switch
        {
            SmartPlaylistField.Title => BuildStringPredicate(m => m.Title!, rule),
            SmartPlaylistField.Genre => BuildGenrePredicate(rule),
            SmartPlaylistField.Year => BuildYearPredicate(rule),
            SmartPlaylistField.Rating => BuildRatingPredicate(rule, userId),
            SmartPlaylistField.PlayCount => BuildPlayCountPredicate(rule, userId),
            SmartPlaylistField.DateAdded => BuildDatePredicate(m => m.Created, rule),
            SmartPlaylistField.LastPlayed => BuildLastPlayedPredicate(rule, userId),
            SmartPlaylistField.IsCompleted => BuildIsCompletedPredicate(rule, userId),
            SmartPlaylistField.ArtistName => BuildArtistNamePredicate(rule),
            SmartPlaylistField.AlbumTitle => BuildAlbumTitlePredicate(rule),
            SmartPlaylistField.TrackNumber => BuildNullableIntPredicate(m => ((MusicTrack)m).TrackNumber, rule),
            SmartPlaylistField.DiscNumber => BuildNullableIntPredicate(m => ((MusicTrack)m).DiscNumber, rule),
            SmartPlaylistField.Bpm => BuildNullableDoublePredicate(m => ((MusicTrack)m).AudioAnalysis!.Bpm, rule),
            SmartPlaylistField.Duration => BuildDurationPredicate(rule),
            SmartPlaylistField.OriginalLanguage => BuildStringPredicate(m => ((Movie)m).OriginalLanguage!, rule),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildStringPredicate(
        Expression<Func<BaseMedia, string>> selector, SmartPlaylistRule rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => Compose(selector, s => s != null && s == value),
            SmartPlaylistOperator.NotEquals => Compose(selector, s => s == null || s != value),
            SmartPlaylistOperator.Contains => Compose(selector, s => s != null && EF.Functions.Like(s, $"%{value}%")),
            SmartPlaylistOperator.IsEmpty => Compose(selector, s => s == null || s == ""),
            SmartPlaylistOperator.IsNotEmpty => Compose(selector, s => s != null && s != ""),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildGenrePredicate(SmartPlaylistRule rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => m => m.Genres.Any(g => g == value),
            SmartPlaylistOperator.NotEquals => m => !m.Genres.Any(g => g == value),
            SmartPlaylistOperator.Contains => m => m.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")),
            SmartPlaylistOperator.IsEmpty => m => !m.Genres.Any(),
            SmartPlaylistOperator.IsNotEmpty => m => m.Genres.Any(),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildYearPredicate(SmartPlaylistRule rule)
    {
        if (!int.TryParse(rule.Value, out var year)) return _ => true;
        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year == year,
            SmartPlaylistOperator.NotEquals => m => m.ReleaseDate == null || m.ReleaseDate.Value.Year != year,
            SmartPlaylistOperator.GreaterThan => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year > year,
            SmartPlaylistOperator.LessThan => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year < year,
            SmartPlaylistOperator.GreaterThanOrEqual => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year >= year,
            SmartPlaylistOperator.LessThanOrEqual => m => m.ReleaseDate != null && m.ReleaseDate.Value.Year <= year,
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildRatingPredicate(SmartPlaylistRule rule, Guid userId)
    {
        if (!double.TryParse(rule.Value, System.Globalization.CultureInfo.InvariantCulture, out var rating))
            return _ => true;

        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value == rating),
            SmartPlaylistOperator.NotEquals => m => !m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value == rating),
            SmartPlaylistOperator.GreaterThan => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value > rating),
            SmartPlaylistOperator.LessThan => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value < rating),
            SmartPlaylistOperator.GreaterThanOrEqual => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value >= rating),
            SmartPlaylistOperator.LessThanOrEqual => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser && r.Value <= rating),
            SmartPlaylistOperator.IsEmpty => m => !m.Ratings.Any(r => r.Source == RatingSource.LocalUser),
            SmartPlaylistOperator.IsNotEmpty => m => m.Ratings.Any(r => r.Source == RatingSource.LocalUser),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildPlayCountPredicate(SmartPlaylistRule rule, Guid userId)
    {
        if (!int.TryParse(rule.Value, out var count)) return _ => true;
        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => m => m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount == count),
            SmartPlaylistOperator.NotEquals => m => !m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount == count),
            SmartPlaylistOperator.GreaterThan => m => m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount > count),
            SmartPlaylistOperator.LessThan => m => m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount < count) || !m.UserMediaStates.Any(s => s.UserId == userId),
            SmartPlaylistOperator.GreaterThanOrEqual => m => m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount >= count),
            SmartPlaylistOperator.LessThanOrEqual => m => m.UserMediaStates.Any(s => s.UserId == userId && s.PlayCount <= count) || !m.UserMediaStates.Any(s => s.UserId == userId),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildDatePredicate(
        Expression<Func<BaseMedia, DateTimeOffset>> selector, SmartPlaylistRule rule)
    {
        if (rule.Operator == SmartPlaylistOperator.InLast && int.TryParse(rule.Value, out var days))
        {
            var threshold = DateTimeOffset.UtcNow.AddDays(-days);
            return Compose(selector, d => d >= threshold);
        }
        return _ => true;
    }

    private static Expression<Func<BaseMedia, bool>> BuildLastPlayedPredicate(SmartPlaylistRule rule, Guid userId)
    {
        if (rule.Operator == SmartPlaylistOperator.InLast && int.TryParse(rule.Value, out var days))
        {
            var threshold = DateTime.UtcNow.AddDays(-days);
            return m => m.UserMediaStates.Any(s => s.UserId == userId && s.LastInteractedAt >= threshold);
        }
        if (rule.Operator == SmartPlaylistOperator.IsEmpty)
            return m => !m.UserMediaStates.Any(s => s.UserId == userId && s.LastInteractedAt != null);
        if (rule.Operator == SmartPlaylistOperator.IsNotEmpty)
            return m => m.UserMediaStates.Any(s => s.UserId == userId && s.LastInteractedAt != null);
        return _ => true;
    }

    private static Expression<Func<BaseMedia, bool>> BuildIsCompletedPredicate(SmartPlaylistRule rule, Guid userId)
    {
        var isCompleted = rule.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => isCompleted
                ? m => m.UserMediaStates.Any(s => s.UserId == userId && s.IsCompleted)
                : m => !m.UserMediaStates.Any(s => s.UserId == userId && s.IsCompleted),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildArtistNamePredicate(SmartPlaylistRule rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => m => ((MusicTrack)m).Artist!.Title == value || ((MusicTrack)m).Album!.Artist!.Title == value,
            SmartPlaylistOperator.NotEquals => m => ((MusicTrack)m).Artist!.Title != value && ((MusicTrack)m).Album!.Artist!.Title != value,
            SmartPlaylistOperator.Contains => m => EF.Functions.Like(((MusicTrack)m).Artist!.Title!, $"%{value}%") || EF.Functions.Like(((MusicTrack)m).Album!.Artist!.Title!, $"%{value}%"),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildAlbumTitlePredicate(SmartPlaylistRule rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => m => ((MusicTrack)m).Album.Title == value,
            SmartPlaylistOperator.NotEquals => m => ((MusicTrack)m).Album.Title != value,
            SmartPlaylistOperator.Contains => m => EF.Functions.Like(((MusicTrack)m).Album.Title!, $"%{value}%"),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildNullableIntPredicate(
        Expression<Func<BaseMedia, int?>> selector, SmartPlaylistRule rule)
    {
        if (!int.TryParse(rule.Value, out var val)) return _ => true;
        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => Compose(selector, n => n != null && n.Value == val),
            SmartPlaylistOperator.NotEquals => Compose(selector, n => n == null || n.Value != val),
            SmartPlaylistOperator.GreaterThan => Compose(selector, n => n != null && n.Value > val),
            SmartPlaylistOperator.LessThan => Compose(selector, n => n != null && n.Value < val),
            SmartPlaylistOperator.GreaterThanOrEqual => Compose(selector, n => n != null && n.Value >= val),
            SmartPlaylistOperator.LessThanOrEqual => Compose(selector, n => n != null && n.Value <= val),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildNullableDoublePredicate(
        Expression<Func<BaseMedia, double?>> selector, SmartPlaylistRule rule)
    {
        if (!double.TryParse(rule.Value, System.Globalization.CultureInfo.InvariantCulture, out var val))
            return _ => true;

        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => Compose(selector, n => n != null && n.Value == val),
            SmartPlaylistOperator.NotEquals => Compose(selector, n => n == null || n.Value != val),
            SmartPlaylistOperator.GreaterThan => Compose(selector, n => n != null && n.Value > val),
            SmartPlaylistOperator.LessThan => Compose(selector, n => n != null && n.Value < val),
            SmartPlaylistOperator.GreaterThanOrEqual => Compose(selector, n => n != null && n.Value >= val),
            SmartPlaylistOperator.LessThanOrEqual => Compose(selector, n => n != null && n.Value <= val),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildDurationPredicate(SmartPlaylistRule rule)
    {
        if (!double.TryParse(rule.Value, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return _ => true;

        var duration = TimeSpan.FromSeconds(seconds);
        return rule.Operator switch
        {
            SmartPlaylistOperator.Equals => m => m.IndexedFiles.Any(f => ((AudioFileMetadata)f.FileMetadata!).Duration == duration),
            SmartPlaylistOperator.GreaterThan => m => m.IndexedFiles.Any(f => ((AudioFileMetadata)f.FileMetadata!).Duration > duration),
            SmartPlaylistOperator.LessThan => m => m.IndexedFiles.Any(f => ((AudioFileMetadata)f.FileMetadata!).Duration < duration),
            SmartPlaylistOperator.GreaterThanOrEqual => m => m.IndexedFiles.Any(f => ((AudioFileMetadata)f.FileMetadata!).Duration >= duration),
            SmartPlaylistOperator.LessThanOrEqual => m => m.IndexedFiles.Any(f => ((AudioFileMetadata)f.FileMetadata!).Duration <= duration),
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

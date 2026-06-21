using System.Linq.Expressions;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Application.Features.Medias.Services;

public static class MediaRuleEvaluator
{
    public static IQueryable<BaseMedia> ApplyFilter(
        IQueryable<BaseMedia> query,
        RuleGroup filter,
        Guid? userId)
    {
        if (filter.Items.Count == 0)
            return query;

        var predicate = BuildGroupPredicate(filter, userId);
        return query.Where(predicate);
    }

    internal static Expression<Func<BaseMedia, bool>> BuildGroupPredicate(RuleGroup group, Guid? userId)
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

    private static Expression<Func<BaseMedia, bool>> BuildRulePredicate(ConditionRuleItem rule, Guid? userId)
    {
        return rule.Field switch
        {
            nameof(SmartPlaylistField.Title) => BuildStringPredicate(m => m.Title!, rule),
            nameof(SmartPlaylistField.Genre) or nameof(RestrictionField.Genre) => BuildGenrePredicate(rule),
            nameof(SmartPlaylistField.Year) or nameof(RestrictionField.ReleaseYear) => BuildYearPredicate(rule),
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
            nameof(SmartPlaylistField.ActorName) => BuildActorNamePredicate(rule),
            nameof(RestrictionField.ContentRating) => BuildContentRatingPredicate(rule),
            "Network" => BuildNetworkPredicate(rule),
            "Studio" => BuildStudioPredicate(rule),
            "InProgress" => BuildInProgressPredicate(userId),
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

    private static Expression<Func<BaseMedia, bool>> BuildActorNamePredicate(ConditionRuleItem rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RuleOperator.Equals => m =>
                m.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name == value)
                || (m is SerieSeason && ((SerieSeason)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name == value))
                || (m is SerieEpisode && ((SerieEpisode)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name == value)),
            RuleOperator.NotEquals => m =>
                !m.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name == value)
                && !(m is SerieSeason && ((SerieSeason)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name == value))
                && !(m is SerieEpisode && ((SerieEpisode)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name == value)),
            RuleOperator.Contains => m =>
                m.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"%{value}%"))
                || (m is SerieSeason && ((SerieSeason)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"%{value}%")))
                || (m is SerieEpisode && ((SerieEpisode)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"%{value}%"))),
            RuleOperator.NotContains => m =>
                !m.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"%{value}%"))
                && !(m is SerieSeason && ((SerieSeason)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"%{value}%")))
                && !(m is SerieEpisode && ((SerieEpisode)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"%{value}%"))),
            RuleOperator.BeginsWith => m =>
                m.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"{value}%"))
                || (m is SerieSeason && ((SerieSeason)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"{value}%")))
                || (m is SerieEpisode && ((SerieEpisode)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"{value}%"))),
            RuleOperator.EndsWith => m =>
                m.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"%{value}"))
                || (m is SerieSeason && ((SerieSeason)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"%{value}")))
                || (m is SerieEpisode && ((SerieEpisode)m).Serie.PersonRoles.Any(r => (r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor) && r.Person.Name != null && EF.Functions.Like(r.Person.Name, $"%{value}"))),
            RuleOperator.IsEmpty => m =>
                !m.PersonRoles.Any(r => r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor)
                && !(m is SerieSeason && ((SerieSeason)m).Serie.PersonRoles.Any(r => r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor))
                && !(m is SerieEpisode && ((SerieEpisode)m).Serie.PersonRoles.Any(r => r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor)),
            RuleOperator.IsNotEmpty => m =>
                m.PersonRoles.Any(r => r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor)
                || (m is SerieSeason && ((SerieSeason)m).Serie.PersonRoles.Any(r => r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor))
                || (m is SerieEpisode && ((SerieEpisode)m).Serie.PersonRoles.Any(r => r.Type == PersonRoleType.Actor || r.Type == PersonRoleType.VoiceActor)),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildGenrePredicate(ConditionRuleItem rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RuleOperator.Equals => m => m.Genres.Any(g => g == value)
                || (m is MusicTrack && ((MusicTrack)m).Album.Genres.Any(g => g == value))
                || (m is SerieSeason && ((SerieSeason)m).Serie.Genres.Any(g => g == value))
                || (m is SerieEpisode && ((SerieEpisode)m).Serie.Genres.Any(g => g == value))
                || (m is MusicArtist && ((MusicArtist)m).Albums.Any(a => a.Genres.Any(g => g == value))),
            RuleOperator.NotEquals => m => !m.Genres.Any(g => g == value)
                && !(m is MusicTrack && ((MusicTrack)m).Album.Genres.Any(g => g == value))
                && !(m is SerieSeason && ((SerieSeason)m).Serie.Genres.Any(g => g == value))
                && !(m is SerieEpisode && ((SerieEpisode)m).Serie.Genres.Any(g => g == value))
                && !(m is MusicArtist && ((MusicArtist)m).Albums.Any(a => a.Genres.Any(g => g == value))),
            RuleOperator.Contains => m => m.Genres.Any(g => EF.Functions.Like(g, $"%{value}%"))
                || (m is MusicTrack && ((MusicTrack)m).Album.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")))
                || (m is SerieSeason && ((SerieSeason)m).Serie.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")))
                || (m is SerieEpisode && ((SerieEpisode)m).Serie.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")))
                || (m is MusicArtist && ((MusicArtist)m).Albums.Any(a => a.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")))),
            RuleOperator.NotContains => m => !m.Genres.Any(g => EF.Functions.Like(g, $"%{value}%"))
                && !(m is MusicTrack && ((MusicTrack)m).Album.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")))
                && !(m is SerieSeason && ((SerieSeason)m).Serie.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")))
                && !(m is SerieEpisode && ((SerieEpisode)m).Serie.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")))
                && !(m is MusicArtist && ((MusicArtist)m).Albums.Any(a => a.Genres.Any(g => EF.Functions.Like(g, $"%{value}%")))),
            RuleOperator.IsEmpty => m => !m.Genres.Any()
                && !(m is MusicTrack && ((MusicTrack)m).Album.Genres.Any())
                && !(m is SerieSeason && ((SerieSeason)m).Serie.Genres.Any())
                && !(m is SerieEpisode && ((SerieEpisode)m).Serie.Genres.Any())
                && !(m is MusicArtist && ((MusicArtist)m).Albums.Any(a => a.Genres.Any())),
            RuleOperator.IsNotEmpty => m => m.Genres.Any()
                || (m is MusicTrack && ((MusicTrack)m).Album.Genres.Any())
                || (m is SerieSeason && ((SerieSeason)m).Serie.Genres.Any())
                || (m is SerieEpisode && ((SerieEpisode)m).Serie.Genres.Any())
                || (m is MusicArtist && ((MusicArtist)m).Albums.Any(a => a.Genres.Any())),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildYearPredicate(ConditionRuleItem rule)
    {
        if (rule.Operator == RuleOperator.IsEmpty)
            return m => m.ReleaseDate == null;
        if (rule.Operator == RuleOperator.IsNotEmpty)
            return m => m.ReleaseDate != null;

        if (!int.TryParse(rule.Value, out var year))
            return _ => true;

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

    private static Expression<Func<BaseMedia, bool>> BuildContentRatingPredicate(ConditionRuleItem rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RuleOperator.Equals => m => (m is Movie && ((Movie)m).ContentRating == value)
                || (m is Serie && ((Serie)m).ContentRating == value),
            RuleOperator.NotEquals => m => !(m is Movie && ((Movie)m).ContentRating == value)
                && !(m is Serie && ((Serie)m).ContentRating == value),
            RuleOperator.Contains => m => (m is Movie && ((Movie)m).ContentRating != null && EF.Functions.Like(((Movie)m).ContentRating!, $"%{value}%"))
                || (m is Serie && ((Serie)m).ContentRating != null && EF.Functions.Like(((Serie)m).ContentRating!, $"%{value}%")),
            RuleOperator.NotContains => m => !(m is Movie && ((Movie)m).ContentRating != null && EF.Functions.Like(((Movie)m).ContentRating!, $"%{value}%"))
                && !(m is Serie && ((Serie)m).ContentRating != null && EF.Functions.Like(((Serie)m).ContentRating!, $"%{value}%")),
            RuleOperator.IsEmpty => m => !(m is Movie && !string.IsNullOrEmpty(((Movie)m).ContentRating))
                && !(m is Serie && !string.IsNullOrEmpty(((Serie)m).ContentRating)),
            RuleOperator.IsNotEmpty => m => (m is Movie && !string.IsNullOrEmpty(((Movie)m).ContentRating))
                || (m is Serie && !string.IsNullOrEmpty(((Serie)m).ContentRating)),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildNetworkPredicate(ConditionRuleItem rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RuleOperator.Equals => m => m is Serie && ((Serie)m).Network == value,
            RuleOperator.NotEquals => m => !(m is Serie) || ((Serie)m).Network != value,
            RuleOperator.Contains => m => m is Serie && ((Serie)m).Network != null && EF.Functions.Like(((Serie)m).Network!, $"%{value}%"),
            RuleOperator.IsEmpty => m => !(m is Serie) || string.IsNullOrEmpty(((Serie)m).Network),
            RuleOperator.IsNotEmpty => m => m is Serie && !string.IsNullOrEmpty(((Serie)m).Network),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildStudioPredicate(ConditionRuleItem rule)
    {
        var value = rule.Value ?? "";
        return rule.Operator switch
        {
            RuleOperator.Equals => m => (m is Movie && ((Movie)m).Studios.Any(s => s == value))
                || (m is Serie && ((Serie)m).Studios.Any(s => s == value)),
            RuleOperator.Contains => m => (m is Movie && ((Movie)m).Studios.Any(s => EF.Functions.Like(s, $"%{value}%")))
                || (m is Serie && ((Serie)m).Studios.Any(s => EF.Functions.Like(s, $"%{value}%"))),
            RuleOperator.IsEmpty => m => !(m is Movie && ((Movie)m).Studios.Any()) && !(m is Serie && ((Serie)m).Studios.Any()),
            RuleOperator.IsNotEmpty => m => (m is Movie && ((Movie)m).Studios.Any()) || (m is Serie && ((Serie)m).Studios.Any()),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildInProgressPredicate(Guid? userId)
    {
        if (userId is null)
            return _ => false;

        var uid = userId.Value;
        return m => !(m is MusicAlbum) && !(m is MusicTrack)
            && m.UserMediaStates.Any(s => s.UserId == uid && !s.IsCompleted && s.LastInteractedAt != null);
    }

    private static Expression<Func<BaseMedia, bool>> BuildRatingPredicate(ConditionRuleItem rule, Guid? userId)
    {
        if (userId is null)
            return _ => true;

        if (!double.TryParse(rule.Value, System.Globalization.CultureInfo.InvariantCulture, out var rating))
            return _ => true;

        var uid = userId.Value;
        return rule.Operator switch
        {
            RuleOperator.Equals => m => m.Ratings.OfType<UserRating>().Any(r => r.UserId == uid && r.Value == rating),
            RuleOperator.NotEquals => m => !m.Ratings.OfType<UserRating>().Any(r => r.UserId == uid && r.Value == rating),
            RuleOperator.GreaterThan => m => m.Ratings.OfType<UserRating>().Any(r => r.UserId == uid && r.Value > rating),
            RuleOperator.LessThan => m => m.Ratings.OfType<UserRating>().Any(r => r.UserId == uid && r.Value < rating),
            RuleOperator.GreaterThanOrEqual => m => m.Ratings.OfType<UserRating>().Any(r => r.UserId == uid && r.Value >= rating),
            RuleOperator.LessThanOrEqual => m => m.Ratings.OfType<UserRating>().Any(r => r.UserId == uid && r.Value <= rating),
            RuleOperator.IsEmpty => m => !m.Ratings.OfType<UserRating>().Any(r => r.UserId == uid),
            RuleOperator.IsNotEmpty => m => m.Ratings.OfType<UserRating>().Any(r => r.UserId == uid),
            _ => _ => true
        };
    }

    private static Expression<Func<BaseMedia, bool>> BuildPlayCountPredicate(ConditionRuleItem rule, Guid? userId)
    {
        if (userId is null || !int.TryParse(rule.Value, out var count))
            return _ => true;

        var uid = userId.Value;
        return rule.Operator switch
        {
            RuleOperator.Equals => m => m.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount == count),
            RuleOperator.NotEquals => m => !m.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount == count),
            RuleOperator.GreaterThan => m => m.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount > count),
            RuleOperator.LessThan => m => m.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount < count) || !m.UserMediaStates.Any(s => s.UserId == uid),
            RuleOperator.GreaterThanOrEqual => m => m.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount >= count),
            RuleOperator.LessThanOrEqual => m => m.UserMediaStates.Any(s => s.UserId == uid && s.PlayCount <= count) || !m.UserMediaStates.Any(s => s.UserId == uid),
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

    private static Expression<Func<BaseMedia, bool>> BuildLastPlayedPredicate(ConditionRuleItem rule, Guid? userId)
    {
        if (userId is null)
            return _ => true;

        var uid = userId.Value;
        if (rule.Operator == RuleOperator.InLast && int.TryParse(rule.Value, out var days))
        {
            var threshold = DateTime.UtcNow.AddDays(-days);
            return m => m.UserMediaStates.Any(s => s.UserId == uid && s.LastInteractedAt >= threshold);
        }

        if (rule.Operator == RuleOperator.IsEmpty)
            return m => !m.UserMediaStates.Any(s => s.UserId == uid && s.LastInteractedAt != null);
        if (rule.Operator == RuleOperator.IsNotEmpty)
            return m => m.UserMediaStates.Any(s => s.UserId == uid && s.LastInteractedAt != null);
        return _ => true;
    }

    private static Expression<Func<BaseMedia, bool>> BuildIsCompletedPredicate(ConditionRuleItem rule, Guid? userId)
    {
        if (userId is null)
            return _ => true;

        var uid = userId.Value;
        var isCompleted = rule.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        return rule.Operator switch
        {
            RuleOperator.Equals => isCompleted
                ? m => m.UserMediaStates.Any(s => s.UserId == uid && s.IsCompleted)
                : m => !m.UserMediaStates.Any(s => s.UserId == uid && s.IsCompleted),
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
        if (!int.TryParse(rule.Value, out var val))
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

    private static Expression<Func<TSource, bool>> Compose<TSource, TMiddle>(
        Expression<Func<TSource, TMiddle>> selector,
        Expression<Func<TMiddle, bool>> predicate)
    {
        var param = selector.Parameters[0];
        var body = new ParameterReplacer(new Dictionary<ParameterExpression, Expression>
        {
            [predicate.Parameters[0]] = selector.Body
        }).Visit(predicate.Body);
        return Expression.Lambda<Func<TSource, bool>>(body, param);
    }

    private static Expression<Func<T, bool>> CombineAnd<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var param = Expression.Parameter(typeof(T), "m");
        var leftBody = new ParameterReplacer(new Dictionary<ParameterExpression, Expression>
        {
            [left.Parameters[0]] = param
        }).Visit(left.Body);
        var rightBody = new ParameterReplacer(new Dictionary<ParameterExpression, Expression>
        {
            [right.Parameters[0]] = param
        }).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), param);
    }

    private static Expression<Func<T, bool>> CombineOr<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var param = Expression.Parameter(typeof(T), "m");
        var leftBody = new ParameterReplacer(new Dictionary<ParameterExpression, Expression>
        {
            [left.Parameters[0]] = param
        }).Visit(left.Body);
        var rightBody = new ParameterReplacer(new Dictionary<ParameterExpression, Expression>
        {
            [right.Parameters[0]] = param
        }).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(leftBody, rightBody), param);
    }

    private sealed class ParameterReplacer(Dictionary<ParameterExpression, Expression> map) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            map.TryGetValue(node, out var replacement) ? replacement : node;
    }
}

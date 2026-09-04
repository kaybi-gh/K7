using System.Linq.Expressions;
using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.Common.QueryExtensions;

/// <summary>
/// Watch state is stored on episodes. Series/season browse filters must aggregate
/// those episode states instead of reading <c>UserMediaStates</c> on the parent.
/// </summary>
internal static class WatchStatePredicates
{
    public static Expression<Func<BaseMedia, bool>> IsCompleted(Guid userId) =>
        m =>
            (m is Serie
                && ((Serie)m).Seasons.SelectMany(season => season.Episodes).Any()
                && ((Serie)m).Seasons.SelectMany(season => season.Episodes)
                    .All(episode => episode.UserMediaStates.Any(state => state.UserId == userId && state.IsCompleted)))
            || (m is SerieSeason
                && ((SerieSeason)m).Episodes.Any()
                && ((SerieSeason)m).Episodes
                    .All(episode => episode.UserMediaStates.Any(state => state.UserId == userId && state.IsCompleted)))
            || (!(m is Serie) && !(m is SerieSeason)
                && m.UserMediaStates.Any(state => state.UserId == userId && state.IsCompleted));

    public static Expression<Func<BaseMedia, bool>> IsNotCompleted(Guid userId) =>
        m =>
            (m is Serie
                && (!((Serie)m).Seasons.SelectMany(season => season.Episodes).Any()
                    || ((Serie)m).Seasons.SelectMany(season => season.Episodes)
                        .Any(episode => !episode.UserMediaStates.Any(state => state.UserId == userId && state.IsCompleted))))
            || (m is SerieSeason
                && (!((SerieSeason)m).Episodes.Any()
                    || ((SerieSeason)m).Episodes
                        .Any(episode => !episode.UserMediaStates.Any(state => state.UserId == userId && state.IsCompleted))))
            || (!(m is Serie) && !(m is SerieSeason)
                && !m.UserMediaStates.Any(state => state.UserId == userId && state.IsCompleted));

    public static Expression<Func<BaseMedia, bool>> IsInProgress(Guid userId) =>
        m => !(m is MusicAlbum) && !(m is MusicTrack)
            && (
                (m is Serie
                    && (((Serie)m).Seasons.SelectMany(season => season.Episodes)
                            .Any(episode => episode.UserMediaStates.Any(state =>
                                state.UserId == userId && !state.IsCompleted && state.LastInteractedAt != null))
                        || (((Serie)m).Seasons.SelectMany(season => season.Episodes)
                                .Any(episode => episode.UserMediaStates.Any(state =>
                                    state.UserId == userId && state.IsCompleted))
                            && ((Serie)m).Seasons.SelectMany(season => season.Episodes)
                                .Any(episode => !episode.UserMediaStates.Any(state =>
                                    state.UserId == userId && state.IsCompleted)))))
                || (m is SerieSeason
                    && (((SerieSeason)m).Episodes
                            .Any(episode => episode.UserMediaStates.Any(state =>
                                state.UserId == userId && !state.IsCompleted && state.LastInteractedAt != null))
                        || (((SerieSeason)m).Episodes
                                .Any(episode => episode.UserMediaStates.Any(state =>
                                    state.UserId == userId && state.IsCompleted))
                            && ((SerieSeason)m).Episodes
                                .Any(episode => !episode.UserMediaStates.Any(state =>
                                    state.UserId == userId && state.IsCompleted)))))
                || (!(m is Serie) && !(m is SerieSeason)
                    && m.UserMediaStates.Any(state =>
                        state.UserId == userId && !state.IsCompleted && state.LastInteractedAt != null))
            );
}

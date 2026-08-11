using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class UserMediaStateMappings
{
    extension(UserMediaState domain)
    {
        public UserMediaStateDto ToUserMediaStateDto() => new()
        {
            LastPlaybackPosition = domain.LastPlaybackPosition,
            ProgressPercentage = domain.ProgressPercentage,
            IsCompleted = domain.IsCompleted,
            PlayCount = domain.PlayCount,
            SkipCount = domain.SkipCount,
            LastInteractedAt = domain.LastInteractedAt
        };
    }

    extension(SharedProfileMediaState domain)
    {
        public UserMediaStateDto ToUserMediaStateDto() => new()
        {
            LastPlaybackPosition = domain.LastPlaybackPosition,
            ProgressPercentage = domain.ProgressPercentage,
            IsCompleted = domain.IsCompleted,
            PlayCount = domain.PlayCount,
            SkipCount = domain.SkipCount,
            LastInteractedAt = domain.LastInteractedAt
        };

        public UserMediaState ToUserMediaState(Guid userId) => new()
        {
            UserId = userId,
            MediaId = domain.MediaId,
            LastPlaybackPosition = domain.LastPlaybackPosition,
            ProgressPercentage = domain.ProgressPercentage,
            IsCompleted = domain.IsCompleted,
            PlayCount = domain.PlayCount,
            SkipCount = domain.SkipCount,
            LastInteractedAt = domain.LastInteractedAt,
            LastKnownDurationSeconds = domain.LastKnownDurationSeconds,
            ExcludedFromContinueWatching = domain.ExcludedFromContinueWatching
        };
    }
}

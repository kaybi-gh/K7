using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Mappings;

public static class UserMediaStateMappings
{
    extension(UserMediaState domain)
    {
        public UserMediaStateDto ToUserMediaStateDto(ItemPlaybackBookmark? bookmark = null) => new()
        {
            LastPlaybackPosition = bookmark?.PositionSeconds ?? 0,
            ProgressPercentage = bookmark?.ProgressPercentage ?? (domain.IsCompleted ? 100 : 0),
            IsCompleted = domain.IsCompleted,
            PlayCount = domain.PlayCount,
            SkipCount = domain.SkipCount,
            LastInteractedAt = domain.LastInteractedAt
        };
    }

    extension(SharedProfileMediaState domain)
    {
        public UserMediaStateDto ToUserMediaStateDto(ItemPlaybackBookmark? bookmark = null) => new()
        {
            LastPlaybackPosition = bookmark?.PositionSeconds ?? 0,
            ProgressPercentage = bookmark?.ProgressPercentage ?? (domain.IsCompleted ? 100 : 0),
            IsCompleted = domain.IsCompleted,
            PlayCount = domain.PlayCount,
            SkipCount = domain.SkipCount,
            LastInteractedAt = domain.LastInteractedAt
        };

        public UserMediaState ToUserMediaState(Guid userId) => new()
        {
            UserId = userId,
            MediaId = domain.MediaId,
            IsCompleted = domain.IsCompleted,
            PlayCount = domain.PlayCount,
            SkipCount = domain.SkipCount,
            LastInteractedAt = domain.LastInteractedAt
        };
    }

    extension(ItemPlaybackBookmark bookmark)
    {
        public UserMediaStateDto ToUserMediaStateDto() => new()
        {
            LastPlaybackPosition = bookmark.PositionSeconds,
            ProgressPercentage = bookmark.ProgressPercentage,
            IsCompleted = false,
            PlayCount = 0,
            SkipCount = 0,
            LastInteractedAt = bookmark.UpdatedAt
        };
    }
}

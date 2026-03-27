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
            LastInteractedAt = domain.LastInteractedAt
        };
    }
}

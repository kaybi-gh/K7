using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Common.Interfaces;

public interface IScrobbleProvider<TMedia> : INotificationHandler<MediaPlaybackCompletedEvent<TMedia>>
    where TMedia : BaseMedia
{
    string Name { get; }
}

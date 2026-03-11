using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Domain.Events;

public class MediaPlaybackCompletedEvent<TMedia>(MediaPlaybackSession session, TMedia media) : BaseEvent
    where TMedia : BaseMedia
{
    public MediaPlaybackSession Session { get; } = session;
    public TMedia Media { get; } = media;

    public static BaseEvent Create(MediaPlaybackSession session, BaseMedia media) => media switch
    {
        MusicTrack m => new MediaPlaybackCompletedEvent<MusicTrack>(session, m),
        Movie m => new MediaPlaybackCompletedEvent<Movie>(session, m),
        Serie m => new MediaPlaybackCompletedEvent<Serie>(session, m),
        SerieSeason m => new MediaPlaybackCompletedEvent<SerieSeason>(session, m),
        SerieEpisode m => new MediaPlaybackCompletedEvent<SerieEpisode>(session, m),
        MusicAlbum m => new MediaPlaybackCompletedEvent<MusicAlbum>(session, m),
        _ => new MediaPlaybackCompletedEvent<BaseMedia>(session, media)
    };
}

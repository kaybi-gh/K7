using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Events;

public class MovieCreatedEvent : BaseEvent
{
    public MovieCreatedEvent(Movie movie)
    {
        Movie = movie;
    }

    public Movie Movie { get; }
}

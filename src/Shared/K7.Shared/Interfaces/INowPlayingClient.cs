using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface INowPlayingClient
{
    Task ReceiveNowPlayingUpdated(IReadOnlyList<NowPlayingSessionDto> sessions);
}

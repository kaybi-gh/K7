using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Interfaces;

public interface INowPlayingNotifier
{
    Task NotifyAsync(string identityUserId, CancellationToken cancellationToken = default);
}

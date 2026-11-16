using K7.Shared.Dtos.Shares;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Whether the signed-in user can be offered "Share" actions. The answer comes from the
/// server (feature switch plus capability, overrides included) and is kept for a short
/// while so menus do not ask on every open.
/// </summary>
public interface IMediaSharingAccess
{
    Task<MediaSharingAvailabilityDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Forget the cached answer, e.g. after the server switch changed.</summary>
    void Invalidate();
}

public sealed class MediaSharingAccess(IMediaShareService shareService) : IMediaSharingAccess
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
    private static readonly MediaSharingAvailabilityDto Unavailable = new();

    private readonly object _lock = new();
    private Task<MediaSharingAvailabilityDto>? _pending;
    private DateTimeOffset _fetchedAt;

    public Task<MediaSharingAvailabilityDto> GetAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_pending is not null && DateTimeOffset.UtcNow - _fetchedAt < CacheDuration)
                return _pending;

            _fetchedAt = DateTimeOffset.UtcNow;
            _pending = FetchAsync(cancellationToken);
            return _pending;
        }
    }

    public void Invalidate()
    {
        lock (_lock)
            _pending = null;
    }

    private async Task<MediaSharingAvailabilityDto> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await shareService.GetSharingAvailabilityAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Sharing is an extra: when the answer is unknown, do not offer it.
            Invalidate();
            return Unavailable;
        }
    }
}

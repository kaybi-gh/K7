namespace K7.Shared.Interfaces;

public interface IRatingService
{
    Task RateMediaAsync(Guid mediaId, int value, CancellationToken cancellationToken = default);
}

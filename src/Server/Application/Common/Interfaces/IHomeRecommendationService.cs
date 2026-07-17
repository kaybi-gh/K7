namespace K7.Server.Application.Common.Interfaces;

public interface IHomeRecommendationService
{
    Task<IReadOnlyList<Guid>> GetRecommendedMediaIdsAsync(
        Guid userId,
        Guid[]? libraryIds,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the display title of the single most recent completed movie or episode used as the
    /// seed for the "Because you watched" row, or null when the user has no eligible seed.
    /// </summary>
    Task<string?> GetBecauseYouWatchedTitleAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns recommendations derived from the single most recent completed movie or episode. Returns
    /// an empty list when the user has no eligible seed or no recommendation data exists for it.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetBecauseYouWatchedMediaIdsAsync(
        Guid userId,
        Guid[]? libraryIds,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}

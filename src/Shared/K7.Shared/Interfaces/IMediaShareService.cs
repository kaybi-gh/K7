using K7.Shared.Dtos.Shares;

namespace K7.Shared.Interfaces;

/// <summary>
/// Managing public share links as a signed-in user.
/// </summary>
public interface IMediaShareService
{
    Task<MediaSharingAvailabilityDto> GetSharingAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <param name="all">Administrators only: include the shares of every user.</param>
    Task<List<MediaShareDto>> GetMediaSharesAsync(bool all = false, Guid? mediaId = null, CancellationToken cancellationToken = default);
    Task<MediaShareDto?> GetMediaShareAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MediaShareLinkDto> CreateMediaShareAsync(CreateMediaShareRequest request, CancellationToken cancellationToken = default);
    Task UpdateMediaShareAsync(Guid id, UpdateMediaShareRequest request, CancellationToken cancellationToken = default);
    Task RevokeMediaShareAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MediaShareLinkDto> RegenerateMediaShareLinkAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteMediaShareAsync(Guid id, CancellationToken cancellationToken = default);
}

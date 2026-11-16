using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Shares;

namespace K7.Shared.Interfaces;

/// <summary>
/// What an anonymous visitor of a public share link can call. It never goes through the
/// signed-in API client: a refused call must not send the visitor to the sign-in page.
/// </summary>
public interface IPublicShareService
{
    /// <summary>Null when the link does not exist (or shares are turned off).</summary>
    Task<OpenShareResultDto?> OpenAsync(string token, string? password, CancellationToken cancellationToken = default);

    /// <summary>Null when the visitor has no valid session for this share anymore.</summary>
    Task<OpenShareResultDto?> GetSessionAsync(Guid shareId, CancellationToken cancellationToken = default);

    /// <summary>Null when the media is not part of the share or the session ended.</summary>
    Task<MediaDto?> GetMediaAsync(Guid shareId, Guid mediaId, CancellationToken cancellationToken = default);

    Task<StreamingSessionDto?> CreateStreamSessionAsync(Guid shareId, CreateShareStreamSessionRequest request, CancellationToken cancellationToken = default);

    Task ReportProgressAsync(Guid shareId, Guid mediaId, double positionSeconds, double durationSeconds, CancellationToken cancellationToken = default);

    string GetDownloadUrl(Guid shareId, Guid indexedFileId);

    Uri? GetAbsoluteUri(string? relativePath);
}

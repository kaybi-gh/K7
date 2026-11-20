using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Shares;
using K7.Shared.Interfaces;

namespace K7.Clients.DesignSystem.Mocks;

// --- Public share mocks ---

public sealed class MockMediaSharingAccess : IMediaSharingAccess
{
    private static readonly MediaSharingAvailabilityDto Available = new()
    {
        Enabled = true,
        CanShare = true,
        IsAdmin = true
    };

    public Task<MediaSharingAvailabilityDto> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Available);

    public void Invalidate()
    {
    }
}

public sealed class MockMediaShareService : IMediaShareService
{
    private readonly List<MediaShareDto> _shares =
    [
        new()
        {
            Id = Guid.NewGuid(),
            RootMediaId = Guid.NewGuid(),
            MediaType = MediaType.Movie,
            MediaTitle = "Dune",
            Label = "For Alice",
            HasPassword = true,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(6),
            MaxAccessCount = 3,
            AccessCount = 1,
            AllowPlayback = true,
            AllowDownload = false,
            Status = MediaShareStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastAccessedAt = DateTimeOffset.UtcNow.AddHours(-3),
            CreatedByUserId = Guid.NewGuid(),
            CreatedByName = "admin"
        }
    ];

    public Task<MediaSharingAvailabilityDto> GetSharingAvailabilityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MediaSharingAvailabilityDto { Enabled = true, CanShare = true, IsAdmin = true });

    public Task<List<MediaShareDto>> GetMediaSharesAsync(bool all = false, Guid? mediaId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_shares.ToList());

    public Task<MediaShareDto?> GetMediaShareAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_shares.FirstOrDefault(share => share.Id == id));

    public Task<MediaShareLinkDto> CreateMediaShareAsync(CreateMediaShareRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MediaShareLinkDto { Id = Guid.NewGuid(), Token = "demo-share-token" });

    public Task UpdateMediaShareAsync(Guid id, UpdateMediaShareRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RevokeMediaShareAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<MediaShareLinkDto> RegenerateMediaShareLinkAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MediaShareLinkDto { Id = id, Token = "demo-share-token" });

    public Task DeleteMediaShareAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class MockPublicShareService : IPublicShareService
{
    public Task<OpenShareResultDto?> OpenAsync(string token, string? password, CancellationToken cancellationToken = default) =>
        Task.FromResult<OpenShareResultDto?>(null);

    public Task<OpenShareResultDto?> GetSessionAsync(Guid shareId, CancellationToken cancellationToken = default) =>
        Task.FromResult<OpenShareResultDto?>(null);

    public Task<MediaDto?> GetMediaAsync(Guid shareId, Guid mediaId, CancellationToken cancellationToken = default) =>
        Task.FromResult<MediaDto?>(null);

    public Task<StreamingSessionDto?> CreateStreamSessionAsync(Guid shareId, CreateShareStreamSessionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<StreamingSessionDto?>(null);

    public Task ReportProgressAsync(Guid shareId, ReportShareProgressRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public string GetDownloadUrl(Guid shareId, Guid indexedFileId) => "#";

    public Uri? GetAbsoluteUri(string? relativePath) => null;
}

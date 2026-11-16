using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Shares;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

/// <summary>
/// What the visitor browser can play, probed once by the share page.
/// </summary>
public sealed record ShareBrowserCapabilities
{
    public IReadOnlyList<string> SupportedContainers { get; init; } = [];
    public IReadOnlyList<string> SupportedAudioCodecs { get; init; } = [];
    public IReadOnlyList<string> SupportedVideoCodecs { get; init; } = [];
    public IReadOnlyList<string> CapabilityTokens { get; init; } = [];
    public bool SupportsHdr { get; init; }
    public double DisplayResolutionWidth { get; init; }
    public double DisplayResolutionHeight { get; init; }
}

/// <summary>
/// State of the public share pages. While one is on screen, every API call of the
/// signed-in client is kept local instead of being sent to the server (a visitor has no
/// account). Once a share is open, the regular players stream through it.
/// </summary>
public interface ISharePlaybackScope
{
    /// <summary>
    /// True from the moment the share layout appears, before any share is open: the
    /// players it hosts load their settings as soon as they are created.
    /// </summary>
    bool IsVisitorPage { get; }

    void BeginVisitorPage();

    void EndVisitorPage();

    Guid? ShareId { get; }

    bool IsActive { get; }

    ShareBrowserCapabilities Capabilities { get; }

    void Enter(Guid shareId, ShareBrowserCapabilities capabilities);

    void Exit();
}

public sealed class SharePlaybackScope : ISharePlaybackScope
{
    public bool IsVisitorPage { get; private set; }

    public void BeginVisitorPage() => IsVisitorPage = true;

    public void EndVisitorPage()
    {
        IsVisitorPage = false;
        ShareId = null;
    }

    public Guid? ShareId { get; private set; }

    public bool IsActive => ShareId is not null;

    public ShareBrowserCapabilities Capabilities { get; private set; } = new();

    public void Enter(Guid shareId, ShareBrowserCapabilities capabilities)
    {
        Capabilities = capabilities;
        ShareId = shareId;
    }

    public void Exit() => ShareId = null;
}

/// <summary>
/// Stream sessions for the regular players. On a public share page the session is created
/// through the share, with the browser capabilities sent along. Anywhere else nothing changes.
/// </summary>
public sealed class ShareAwareStreamUriService(
    IStreamUriService inner,
    ISharePlaybackScope scope,
    IPublicShareService publicShare) : IStreamUriService
{
    public async Task<StreamingSessionDto> GetOrCreateSessionAsync(
        Guid indexedFileId,
        int? audioTrackIndex = null,
        int? subtitleTrackIndex = null,
        double? startSeconds = null,
        CancellationToken cancellationToken = default)
    {
        if (scope.ShareId is not { } shareId)
            return await inner.GetOrCreateSessionAsync(indexedFileId, audioTrackIndex, subtitleTrackIndex, startSeconds, cancellationToken);

        var capabilities = scope.Capabilities;
        var session = await publicShare.CreateStreamSessionAsync(
                shareId,
                new CreateShareStreamSessionRequest
                {
                    IndexedFileId = indexedFileId,
                    AudioTrackIndex = audioTrackIndex,
                    SubtitleTrackIndex = subtitleTrackIndex,
                    StartSeconds = startSeconds is > 1 ? startSeconds : null,
                    SupportedContainers = capabilities.SupportedContainers,
                    SupportedAudioCodecs = capabilities.SupportedAudioCodecs,
                    SupportedVideoCodecs = capabilities.SupportedVideoCodecs,
                    CapabilityTokens = capabilities.CapabilityTokens,
                    SupportsHdr = capabilities.SupportsHdr,
                    DisplayResolutionWidth = capabilities.DisplayResolutionWidth,
                    DisplayResolutionHeight = capabilities.DisplayResolutionHeight
                },
                cancellationToken)
            ?? throw new InvalidOperationException($"No streaming session created for IndexedFile with id '{indexedFileId}'.");

        if (session.Source is not null)
            session.Source.Uri = publicShare.GetAbsoluteUri(session.Source.Uri.OriginalString)!;

        return session;
    }

    // Files hosted on other servers are never part of a share.
    public Task<StreamingSessionDto?> GetOrCreateRemoteSessionAsync(
        Guid remoteFileId,
        int? audioTrackIndex = null,
        int? subtitleTrackIndex = null,
        CancellationToken cancellationToken = default) =>
        scope.IsActive
            ? Task.FromResult<StreamingSessionDto?>(null)
            : inner.GetOrCreateRemoteSessionAsync(remoteFileId, audioTrackIndex, subtitleTrackIndex, cancellationToken);
}

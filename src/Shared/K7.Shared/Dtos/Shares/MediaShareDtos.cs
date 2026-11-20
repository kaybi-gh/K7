using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Shares;

public sealed record MediaShareDto
{
    public Guid Id { get; init; }
    public Guid RootMediaId { get; init; }
    public MediaType MediaType { get; init; }

    /// <summary>Display title of the shared media (never null: falls back to numbering).</summary>
    public string MediaTitle { get; init; } = string.Empty;

    /// <summary>Parent context, e.g. the series of an episode or the artist of an album.</summary>
    public string? MediaSubtitle { get; init; }
    public string? PictureUri { get; init; }

    public string? Label { get; init; }
    public bool HasPassword { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public int? MaxAccessCount { get; init; }
    public int AccessCount { get; init; }
    public bool AllowPlayback { get; init; }
    public bool AllowDownload { get; init; }
    public bool CloseWhenConsumed { get; init; }

    public MediaShareStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ConsumedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public DateTimeOffset? LastAccessedAt { get; init; }

    public Guid CreatedByUserId { get; init; }
    public string? CreatedByName { get; init; }

    /// <summary>Only filled when a single share is requested.</summary>
    public IReadOnlyList<MediaShareSessionDto> Sessions { get; init; } = [];
}

public sealed record MediaShareSessionDto
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public sealed record CreateMediaShareRequest
{
    public Guid MediaId { get; init; }
    public string? Label { get; init; }
    public string? Password { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public int? MaxAccessCount { get; init; }
    public bool AllowPlayback { get; init; } = true;
    public bool AllowDownload { get; init; }
    public bool CloseWhenConsumed { get; init; }
}

public sealed record UpdateMediaShareRequest
{
    public string? Label { get; init; }

    /// <summary>
    /// Null keeps the current password, an empty string removes it, anything else replaces it.
    /// </summary>
    public string? Password { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public int? MaxAccessCount { get; init; }
    public bool AllowPlayback { get; init; } = true;
    public bool AllowDownload { get; init; }
    public bool CloseWhenConsumed { get; init; }
}

/// <summary>
/// The link token is only returned here: the server keeps a hash and cannot show it again.
/// </summary>
public sealed record MediaShareLinkDto
{
    public Guid Id { get; init; }
    public string Token { get; init; } = string.Empty;
}

/// <summary>
/// The link token travels in the request body, never in a URL: paths end up in logs.
/// </summary>
public sealed record OpenShareRequest
{
    public string Token { get; init; } = string.Empty;
    public string? Password { get; init; }
}

public enum OpenShareOutcome
{
    Opened,
    PasswordRequired,
    InvalidPassword,
    Unavailable,

    /// <summary>Too many wrong passwords: the share accepts none for a while.</summary>
    PasswordLocked
}

public sealed record OpenShareResultDto
{
    public OpenShareOutcome Outcome { get; init; }

    /// <summary>Why the share cannot be opened, when <see cref="Outcome"/> is unavailable.</summary>
    public MediaShareStatus Status { get; init; }

    public Guid? ShareId { get; init; }
    public Guid? RootMediaId { get; init; }
    public MediaType? RootMediaType { get; init; }
    public bool AllowPlayback { get; init; }
    public bool AllowDownload { get; init; }
    public DateTimeOffset? SessionExpiresAt { get; init; }
}

public sealed record CreateShareStreamSessionRequest
{
    public Guid IndexedFileId { get; init; }
    public int? AudioTrackIndex { get; init; }
    public int? SubtitleTrackIndex { get; init; }
    public double? StartSeconds { get; init; }

    /// <summary>
    /// What the visitor browser can play, as probed in the page. The server matches these
    /// against its format catalog, the way a registered web device does on its own.
    /// </summary>
    public IReadOnlyList<string> SupportedContainers { get; init; } = [];
    public IReadOnlyList<string> SupportedAudioCodecs { get; init; } = [];
    public IReadOnlyList<string> SupportedVideoCodecs { get; init; } = [];

    /// <summary>Extra capability tokens: decoder profiles and audio output channels.</summary>
    public IReadOnlyList<string> CapabilityTokens { get; init; } = [];
    public bool SupportsHdr { get; init; }
    public double DisplayResolutionWidth { get; init; }
    public double DisplayResolutionHeight { get; init; }
}

public sealed record ReportShareProgressRequest
{
    public Guid MediaId { get; init; }
    public double PositionSeconds { get; init; }
    public double DurationSeconds { get; init; }

    /// <summary>Stream session being played. Without it the report only serves "close once consumed".</summary>
    public Guid? SessionId { get; init; }
    public Guid? ReferenceId { get; init; }

    /// <summary>Player state, as <c>PlaybackState</c>.</summary>
    public int? State { get; init; }
    public Guid? IndexedFileId { get; init; }
    public int? AudioTrackIndex { get; init; }
    public int? SubtitleTrackIndex { get; init; }
}

/// <summary>
/// Whether the current user can be offered sharing.
/// </summary>
public sealed record MediaSharingAvailabilityDto
{
    /// <summary>Public shares are turned on for this server.</summary>
    public bool Enabled { get; init; }

    /// <summary>The user holds the share capability.</summary>
    public bool CanShare { get; init; }
    public bool IsAdmin { get; init; }

    public bool CanCreate => Enabled && CanShare;
}

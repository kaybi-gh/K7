namespace K7.Shared.Dtos.Scrobbling;

public sealed record ScrobblingSettingsDto
{
    public bool Enabled { get; init; } = true;
    public string LastFmApiKey { get; init; } = "";
    public string LastFmApiSecret { get; init; } = "";
    public string LastFmApiHost { get; init; } = "https://ws.audioscrobbler.com/2.0/";
    public string TraktClientId { get; init; } = "";
    public string TraktClientSecret { get; init; } = "";
}

public sealed record ScrobblingAvailabilityDto
{
    public bool Enabled { get; init; } = true;
    public bool LastFmConfigured { get; init; }
    public bool TraktConfigured { get; init; }
}

public sealed record UserScrobblerAccountDto
{
    public required Guid Id { get; init; }
    public required string Provider { get; init; }
    public required bool IsEnabled { get; init; }
    public IReadOnlyList<string> MediaTypes { get; init; } = [];
    public required bool IncludeNowPlaying { get; init; }
    public string? DisplayName { get; init; }
    public string? PresetId { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    public IReadOnlyList<string> WebhookEvents { get; init; } = [];
    public string? PlayTemplate { get; init; }
    public string? PauseTemplate { get; init; }
    public string? StopTemplate { get; init; }
    public string? ProgressTemplate { get; init; }
    public string? ScrobbleTemplate { get; init; }
    public required DateTimeOffset Created { get; init; }
}

public sealed record CreateUserScrobblerAccountRequest
{
    public required string Provider { get; init; }
    public string? DisplayName { get; init; }
    public IReadOnlyList<string> MediaTypes { get; init; } = [];
    public bool IncludeNowPlaying { get; init; } = true;
    public string? Token { get; init; }
    public string? Username { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    public string? PresetId { get; init; }
    public IReadOnlyList<string> WebhookEvents { get; init; } = [];
    public string? PlayTemplate { get; init; }
    public string? PauseTemplate { get; init; }
    public string? StopTemplate { get; init; }
    public string? ProgressTemplate { get; init; }
    public string? ScrobbleTemplate { get; init; }
}

public sealed record UpdateUserScrobblerAccountRequest
{
    public bool IsEnabled { get; init; }
    public IReadOnlyList<string> MediaTypes { get; init; } = [];
    public bool IncludeNowPlaying { get; init; }
    public string? DisplayName { get; init; }
    public string? Token { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    public string? PresetId { get; init; }
    public IReadOnlyList<string>? WebhookEvents { get; init; }
    public string? PlayTemplate { get; init; }
    public string? PauseTemplate { get; init; }
    public string? StopTemplate { get; init; }
    public string? ProgressTemplate { get; init; }
    public string? ScrobbleTemplate { get; init; }
}

public sealed record ScrobbleWebhookPresetDto
{
    public required string Id { get; init; }
    public required string DisplayNameKey { get; init; }
    public required string UrlHint { get; init; }
    /// <summary>
    /// Optional public page where the user finds a token or webhook URL (hosted providers).
    /// </summary>
    public string? SetupHelpUrl { get; init; }
    public required string PlayTemplate { get; init; }
    public required string PauseTemplate { get; init; }
    public required string StopTemplate { get; init; }
    public required string ProgressTemplate { get; init; }
    public required string ScrobbleTemplate { get; init; }
    /// <summary>
    /// Media types this preset can scrobble. Empty means Movie, SerieEpisode, and MusicTrack.
    /// </summary>
    public IReadOnlyList<string> MediaTypes { get; init; } = [];
    /// <summary>
    /// Default webhook events when creating an account from this preset. Empty means all events.
    /// </summary>
    public IReadOnlyList<string> DefaultEvents { get; init; } = [];
}

public sealed record LastFmAuthStartDto
{
    public required string Token { get; init; }
    public required string AuthUrl { get; init; }
}

public sealed record TraktDeviceStartDto
{
    public required string DeviceCode { get; init; }
    public required string UserCode { get; init; }
    public required string VerificationUrl { get; init; }
    public required int Interval { get; init; }
    public required int ExpiresIn { get; init; }
}

public sealed record ScrobbleTestResultDto
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
}

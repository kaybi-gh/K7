namespace K7.Shared.Dtos;

public sealed record MusicIntelligenceSettingsDto
{
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Enables AudioMuse Instant Playlist (chat / "Par description"). Requires a third-party LLM on AudioMuse.
    /// Defaults to false when absent from stored JSON.
    /// </summary>
    public bool InstantPlaylistEnabled { get; init; }
}

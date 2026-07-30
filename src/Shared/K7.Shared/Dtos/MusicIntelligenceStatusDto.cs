namespace K7.Shared.Dtos;

public sealed record MusicIntelligenceStatusDto
{
    public bool IsEnabled { get; init; }
    public bool IsAvailable { get; init; }

    /// <summary>Admin toggle for Instant Playlist / "Par description".</summary>
    public bool InstantPlaylistEnabled { get; init; }

    /// <summary>True when music intelligence is available and Instant Playlist is enabled in settings.</summary>
    public bool InstantPlaylistAvailable { get; init; }
}

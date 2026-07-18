namespace K7.Server.Application.Common.Interfaces;

public interface IThemeSongService
{
    /// <summary>
    /// Resolves a playable theme file path: library sidecar first, then generated metadata MP3.
    /// </summary>
    Task<string?> ResolvePlayablePathAsync(Guid mediaId, CancellationToken cancellationToken = default);

    Task<bool> HasThemeSongAsync(Guid mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts a light faded MP3 from an Intro segment into Metadatas when no library sidecar exists.
    /// Serie only. No-op if sidecar or generated file already present, intro detection is off, or no Intro segment.
    /// </summary>
    Task ExtractSerieThemeAsync(Guid serieId, CancellationToken cancellationToken = default);
}

using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.UI.Components.Shares;

/// <summary>
/// A visitor asked to watch a movie or an episode of the share.
/// </summary>
/// <param name="IndexedFileId">A specific version of the media. Null plays its first file.</param>
public sealed record ShareVideoPlayRequest(Guid MediaId, Guid? IndexedFileId = null);

/// <summary>
/// A visitor asked to listen to tracks of the share. The queue only ever holds shared tracks.
/// </summary>
public sealed record ShareAudioPlayRequest(IReadOnlyList<AudioQueueItem> Queue, int StartIndex);

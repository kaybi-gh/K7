namespace K7.Clients.Shared.Interfaces;

public interface IMediaBrowseHubCoordinator
{
    /// <summary>
    /// Subscribes to library browse hub events.
    /// <paramref name="onCatalogChanged"/> runs for membership changes (scan, batch add, indexed files).
    /// <paramref name="onMediaVisualChanged"/> runs for metadata/picture updates on a single media id.
    /// When <paramref name="onMediaVisualChanged"/> is null, visual updates fall back to <paramref name="onCatalogChanged"/>.
    /// </summary>
    IDisposable Subscribe(
        Guid[]? libraryIds,
        Guid[]? libraryGroupIds,
        Action onCatalogChanged,
        Action<Guid>? onMediaVisualChanged = null);
}

namespace K7.Clients.MAUI.Interfaces;

/// <summary>
/// Platform hook that keeps the process alive while user downloads are active.
/// Android starts a dataSync foreground service; other platforms are no-ops.
/// </summary>
public interface IDownloadKeepAlive
{
    void SetActive(bool active);
}

using K7.Clients.MAUI.Interfaces;

namespace K7.Clients.MAUI.Services;

public sealed class NoOpDownloadKeepAlive : IDownloadKeepAlive
{
    public void SetActive(bool active)
    {
    }
}

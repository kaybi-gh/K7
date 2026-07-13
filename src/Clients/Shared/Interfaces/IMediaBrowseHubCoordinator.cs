namespace K7.Clients.Shared.Interfaces;

public interface IMediaBrowseHubCoordinator
{
    IDisposable Subscribe(Guid[]? libraryIds, Guid[]? libraryGroupIds, Action onRefresh);
}

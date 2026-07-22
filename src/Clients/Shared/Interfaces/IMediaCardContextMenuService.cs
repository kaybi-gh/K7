using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IMediaCardContextMenuService
{
    MediaCardContextMenuRequest? Current { get; }

    event Action? Changed;

    void Open(MediaCardContextMenuRequest request);

    void Close();
}

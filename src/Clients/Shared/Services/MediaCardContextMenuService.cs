using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Services;

public sealed class MediaCardContextMenuService : IMediaCardContextMenuService
{
    public MediaCardContextMenuRequest? Current { get; private set; }

    public event Action? Changed;

    public void Open(MediaCardContextMenuRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Current = request;
        Changed?.Invoke();
    }

    public void Close()
    {
        if (Current is null)
            return;

        Current = null;
        Changed?.Invoke();
    }
}

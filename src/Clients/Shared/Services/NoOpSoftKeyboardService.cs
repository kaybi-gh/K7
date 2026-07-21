using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public sealed class NoOpSoftKeyboardService : ISoftKeyboardService
{
    public void Show()
    {
    }

    public void Hide()
    {
    }
}

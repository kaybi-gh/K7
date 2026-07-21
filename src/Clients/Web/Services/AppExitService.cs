using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Web.Services;

public sealed class AppExitService : IAppExitService
{
    public void Exit()
    {
        // Web clients do not exit the host process from Escape.
    }
}

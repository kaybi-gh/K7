using System.Diagnostics;
using K7.Clients.MAUI.Data;

namespace K7.Clients.MAUI.Services.Authentication;

public class MauiDatabaseInitializer : IMauiInitializeScopedService
{
    public void Initialize(IServiceProvider services)
    {
        try
        {
            OpenIddictDbBootstrap.Start(services);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - MauiDatabaseInitializer - ERROR: {ex}");
        }
    }
}

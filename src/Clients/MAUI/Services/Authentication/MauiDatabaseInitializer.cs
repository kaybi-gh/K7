using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace K7.Clients.MAUI.Services.Authentication;

public class MauiDatabaseInitializer : IMauiInitializeScopedService
{
    public void Initialize(IServiceProvider services)
    {
        // Run off the main thread to avoid ANR on Android.
        // The DB will be ready before the user triggers any auth flow.
        Task.Run(() =>
        {
            try
            {
                Debug.WriteLine("K7 MAUI - MauiDatabaseInitializer - Starting EnsureCreated");
                using var scope = services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<OpenIddictDbContext>();
                context.Database.EnsureCreated();
                Debug.WriteLine("K7 MAUI - MauiDatabaseInitializer - EnsureCreated completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"K7 MAUI - MauiDatabaseInitializer - ERROR: {ex}");
            }
        });
    }
}

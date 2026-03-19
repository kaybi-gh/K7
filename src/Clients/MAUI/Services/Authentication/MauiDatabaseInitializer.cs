using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace K7.Clients.MAUI.Services.Authentication;

public class MauiDatabaseInitializer : IMauiInitializeScopedService
{
    public void Initialize(IServiceProvider services)
    {
        try
        {
            Debug.WriteLine("K7 MAUI - MauiDatabaseInitializer - Starting EnsureCreated");
            var context = services.GetRequiredService<DbContext>();
            context.Database.EnsureCreated();
            Debug.WriteLine("K7 MAUI - MauiDatabaseInitializer - EnsureCreated completed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - MauiDatabaseInitializer - ERROR: {ex}");
        }
    }
}

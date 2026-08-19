using K7.Clients.MAUI.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.MAUI.Data;

/// <summary>
/// Creates the OpenIddict SQLite schema off the UI thread. Solo restore must await
/// <see cref="Ready"/> before the token endpoint so it does not race EnsureCreated.
/// </summary>
internal static class OpenIddictDbBootstrap
{
    private static readonly object Gate = new();
    private static bool _started;

    public static Task Ready { get; private set; } = Task.CompletedTask;

    public static void Start(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        lock (Gate)
        {
            if (_started)
                return;

            _started = true;
            Ready = Task.Run(() => Initialize(services));
        }
    }

    private static void Initialize(IServiceProvider services)
    {
        try
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OpenIddictDbContext>();
            context.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"K7 MAUI - OpenIddictDbBootstrap failed: {ex}");
        }
    }
}

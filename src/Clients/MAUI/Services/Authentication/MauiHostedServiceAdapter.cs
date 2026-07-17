using Microsoft.Extensions.Hosting;

namespace K7.Clients.MAUI.Services.Authentication;

public class MauiHostedServiceAdapter : IMauiInitializeService
{
    private readonly IHostedService _service;
    private readonly IHostApplicationLifetime? _lifetime;

    public MauiHostedServiceAdapter(IHostedService service, IHostApplicationLifetime? lifetime = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _lifetime = lifetime;
    }

    public void Initialize(IServiceProvider services)
    {
        // Use ApplicationStopping (rather than CancellationToken.None) so startup
        // aborts promptly if the app begins shutting down before it completes.
        var cancellationToken = _lifetime?.ApplicationStopping ?? CancellationToken.None;
        _ = Task.Run(async () =>
        {
            try
            {
                await _service.StartAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"K7 MAUI - Hosted service initialization failed: {ex}");
            }
        });
    }
}

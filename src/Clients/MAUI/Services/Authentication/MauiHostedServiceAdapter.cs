using Microsoft.Extensions.Hosting;

namespace K7.Clients.MAUI.Services.Authentication;

public class MauiHostedServiceAdapter : IMauiInitializeService
{
    private readonly IHostedService _service;

    public MauiHostedServiceAdapter(IHostedService service)
        => _service = service ?? throw new ArgumentNullException(nameof(service));

    public void Initialize(IServiceProvider services)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _service.StartAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"K7 MAUI - Hosted service initialization failed: {ex}");
            }
        });
    }
}

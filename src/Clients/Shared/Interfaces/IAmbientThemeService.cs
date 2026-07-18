namespace K7.Clients.Shared.Interfaces;

public interface IAmbientThemeService
{
    Task PlayAsync(string themeUrl, double volume = 0.25, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

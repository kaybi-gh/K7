using K7.Clients.Shared.Interfaces;

namespace K7.Clients.MAUI.Services;

public sealed class MauiExternalLinkService : IExternalLinkService
{
    public Task<bool> OpenAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            return Task.FromResult(false);

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                return await Launcher.Default.OpenAsync(uri);
            }
            catch (Exception)
            {
                return false;
            }
        });
    }
}

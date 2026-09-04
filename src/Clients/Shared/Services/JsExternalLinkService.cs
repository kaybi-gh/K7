using K7.Clients.Shared.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

public sealed class JsExternalLinkService(IJSRuntime js) : IExternalLinkService
{
    public async Task<bool> OpenAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            return false;

        try
        {
            return await js.InvokeAsync<bool>("K7.openExternalUrl", cancellationToken, uri.AbsoluteUri);
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
        catch (JSException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

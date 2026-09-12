using System.Globalization;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.MAUI.Platforms.Windows;

public sealed class MpcWebClient
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    public async Task<MpcPlayerVariables?> GetVariablesAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        var html = await GetStringAsync(host, port, "variables.html", cancellationToken);
        return MpcVariablesParser.TryParse(html);
    }

    public async Task<bool> SeekPercentAsync(
        string host,
        int port,
        double percent,
        CancellationToken cancellationToken = default)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        var query = "command.html?wm_command=-1&percent="
            + clamped.ToString("0.###", CultureInfo.InvariantCulture);
        var body = await GetStringAsync(host, port, query, cancellationToken);
        return body is not null;
    }

    private async Task<string?> GetStringAsync(
        string host,
        int port,
        string relativePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"http://{host}:{port}/{relativePath.TrimStart('/')}";
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}

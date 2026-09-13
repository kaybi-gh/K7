using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class AuthComplete
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private string _title = "";
    private string _message = "";
    private bool _isError;
    private string? _launchUri;

    protected override void OnInitialized()
    {
        var status = GetQueryValue("status");
        _launchUri = ResolveLaunchUri(GetQueryValue("launch"));

        (_title, _message, _isError) = status?.ToLowerInvariant() switch
        {
            "denied" => (L["DeniedTitle"].Value, L["DeniedMessage"].Value, true),
            "error" => (L["ErrorTitle"].Value, L["ErrorMessage"].Value, true),
            _ => (L["SuccessTitle"].Value, L["SuccessMessage"].Value, false)
        };
    }

    internal static string? ResolveLaunchUri(string? raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "k7", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, "callback", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.Equals(uri.AbsolutePath, "/login", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.AbsolutePath, "/logout", StringComparison.OrdinalIgnoreCase))
            return null;

        return uri.AbsoluteUri;
    }

    private string? GetQueryValue(string key)
    {
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
            return null;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator < 0)
                continue;

            var name = Uri.UnescapeDataString(part[..separator]);
            if (!name.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            return Uri.UnescapeDataString(part[(separator + 1)..]);
        }

        return null;
    }
}

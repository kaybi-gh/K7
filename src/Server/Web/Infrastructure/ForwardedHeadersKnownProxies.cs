using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace K7.Server.Web.Infrastructure;

public static class ForwardedHeadersKnownProxies
{
    // RFC1918 + loopback, plus IPv4-mapped IPv6 forms (Docker often presents ::ffff:x.x.x.x).
    private static readonly string[] PrivateNetworkCidrs =
    [
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "127.0.0.0/8",
        "::1/128",
        "fc00::/7",
        "::ffff:10.0.0.0/104",
        "::ffff:172.16.0.0/108",
        "::ffff:192.168.0.0/112",
        "::ffff:127.0.0.0/104"
    ];

    public static void Configure(
        ForwardedHeadersOptions options,
        string[]? entries,
        bool trustPrivateProxies,
        bool isDevelopment)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        if (TryAddExplicitEntries(options, entries))
            return;

        if (trustPrivateProxies)
        {
            AddPrivateNetworks(options);
            return;
        }

        if (isDevelopment)
        {
            // Development without private trust: keep forwarded-proto enabled for local tests.
            return;
        }

        options.ForwardedHeaders = ForwardedHeaders.None;
    }

    private static bool TryAddExplicitEntries(ForwardedHeadersOptions options, string[]? entries)
    {
        var configured = false;
        if (entries is not { Length: > 0 })
            return false;

        foreach (var raw in entries)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var entry = raw.Trim();
            if (System.Net.IPNetwork.TryParse(entry, out var network))
            {
                options.KnownIPNetworks.Add(network);
                configured = true;
                continue;
            }

            if (IPAddress.TryParse(entry, out var address))
            {
                options.KnownProxies.Add(address);
                configured = true;
            }
        }

        return configured;
    }

    private static void AddPrivateNetworks(ForwardedHeadersOptions options)
    {
        foreach (var cidr in PrivateNetworkCidrs)
        {
            if (System.Net.IPNetwork.TryParse(cidr, out var network))
                options.KnownIPNetworks.Add(network);
        }
    }
}

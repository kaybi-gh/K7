using System.Net;
using System.Net.Sockets;

namespace K7.Server.Application.Common.Security;

public sealed class PeerUrlValidationOptions
{
    public bool AllowInsecureHttp { get; init; }
    public bool BlockPrivateNetworks { get; init; }
}

public static class PeerUrlValidator
{
    public static void ValidateOutgoingUrl(string url, PeerUrlValidationOptions options)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Peer URL is invalid.");

        if (uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Peer URL must use HTTP or HTTPS.");

        if (!options.AllowInsecureHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Peer URL must use HTTPS.");

        if (string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("Peer URL must include a host.");

        if (uri.Host is "localhost" or "127.0.0.1" or "::1")
            throw new InvalidOperationException("Peer URL must not target loopback.");

        if (uri.Host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Peer URL must not target cloud metadata endpoints.");

        ValidateResolvedAddresses(uri.Host, options);
    }

    private static void ValidateResolvedAddresses(string host, PeerUrlValidationOptions options)
    {
        IPAddress[] addresses;
        try
        {
            addresses = Dns.GetHostAddresses(host);
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException("Peer URL host could not be resolved.", ex);
        }

        if (addresses.Length == 0)
            throw new InvalidOperationException("Peer URL host could not be resolved.");

        foreach (var address in addresses)
        {
            if (IPAddress.IsLoopback(address))
                throw new InvalidOperationException("Peer URL must not resolve to loopback.");

            if (IsLinkLocal(address))
                throw new InvalidOperationException("Peer URL must not resolve to a link-local address.");

            if (options.BlockPrivateNetworks && IsPrivateNetwork(address))
                throw new InvalidOperationException("Peer URL must not resolve to a private network address.");
        }
    }

    private static bool IsLinkLocal(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal;

        var ipv4 = address.MapToIPv4().GetAddressBytes();
        return ipv4[0] == 169 && ipv4[1] == 254;
    }

    private static bool IsPrivateNetwork(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6UniqueLocal;

        var ipv4 = address.MapToIPv4().GetAddressBytes();
        return ipv4[0] switch
        {
            10 => true,
            172 when ipv4[1] >= 16 && ipv4[1] <= 31 => true,
            192 when ipv4[1] == 168 => true,
            _ => false
        };
    }
}

using System.Net;
using System.Net.Sockets;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// HTTP handler for the configured K7 server.
/// A hostname often has a LAN or VPN address and a public address that is only a proxy.
/// The proxy accepts TCP and answers HTTP 521, so the default client keeps that socket
/// and never tries the address that actually reaches K7. Private and VPN addresses are
/// tried first (IPv4, then IPv6), then public IPv4, then public IPv6. System HTTP proxies
/// are ignored so a VPN proxy cannot replace that choice.
/// </summary>
public static class K7ServerSockets
{
    private static readonly TimeSpan PerAddressConnectTimeout = TimeSpan.FromSeconds(5);

    public static SocketsHttpHandler Create(DecompressionMethods decompression = DecompressionMethods.None)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = decompression
        };
        Configure(handler);
        return handler;
    }

    public static void Configure(SocketsHttpHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler.UseProxy = false;
        handler.ConnectCallback = ConnectAsync;
    }

    internal static IPAddress[] Order(IReadOnlyList<IPAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return addresses
            .Distinct()
            .OrderBy(Rank)
            .ThenBy(static address => address.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = Order(await ResolveAsync(context.DnsEndPoint.Host, cancellationToken));
        Exception? last = null;

        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            var connected = false;
            try
            {
                using var perAddress = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                perAddress.CancelAfter(PerAddressConnectTimeout);
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), perAddress.Token);
                connected = true;
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                last = ex;
            }
            finally
            {
                if (!connected)
                    socket.Dispose();
            }
        }

        throw last ?? new SocketException((int)SocketError.HostNotFound);
    }

    private static async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var literal))
            return [literal];

        return await Dns.GetHostAddressesAsync(host, cancellationToken);
    }

    private static int Rank(IPAddress address)
    {
        var comparable = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        var locality = IsLocalOrPrivate(comparable) ? 0 : 2;
        var family = comparable.AddressFamily == AddressFamily.InterNetwork ? 0 : 1;
        return locality + family;
    }

    private static bool IsLocalOrPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,
                127 => true,
                172 => bytes[1] is >= 16 and <= 31,
                192 => bytes[1] == 168,
                169 => bytes[1] == 254,
                // Carrier-grade NAT, including Tailscale.
                100 => bytes[1] is >= 64 and <= 127,
                _ => false
            };
        }

        return address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal;
    }
}

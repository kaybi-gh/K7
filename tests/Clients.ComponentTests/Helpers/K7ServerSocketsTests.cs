using System.Net;
using System.Net.Sockets;
using System.Text;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class K7ServerSocketsTests
{
    [Test]
    public void Order_ShouldTryLanAndVpnAddresses_BeforePublicOnes()
    {
        var ordered = K7ServerSockets.Order(
        [
            IPAddress.Parse("2606:4700::1"),
            IPAddress.Parse("104.21.0.1"),
            IPAddress.Parse("10.8.0.5"),
            IPAddress.Parse("100.64.0.2"),
            IPAddress.Parse("192.168.1.10"),
            IPAddress.Parse("fd7a:115c:a1e0::1")
        ]);

        ordered.Select(static address => address.ToString()).Should().Equal(
            "10.8.0.5",
            "100.64.0.2",
            "192.168.1.10",
            "fd7a:115c:a1e0::1",
            "104.21.0.1",
            "2606:4700::1");
    }

    [Test]
    public void Order_ShouldKeepCarrierGradeNatInsideTheVpnRange()
    {
        var ordered = K7ServerSockets.Order(
        [
            IPAddress.Parse("100.128.0.1"),
            IPAddress.Parse("100.63.255.255"),
            IPAddress.Parse("100.64.0.1"),
            IPAddress.Parse("100.127.255.255"),
            IPAddress.Parse("172.15.255.255"),
            IPAddress.Parse("172.16.0.1"),
            IPAddress.Parse("172.31.255.255"),
            IPAddress.Parse("172.32.0.1")
        ]);

        ordered.Select(static address => address.ToString()).Should().Equal(
            "100.127.255.255",
            "100.64.0.1",
            "172.16.0.1",
            "172.31.255.255",
            "100.128.0.1",
            "100.63.255.255",
            "172.15.255.255",
            "172.32.0.1");
    }

    [Test]
    public async Task Create_ShouldUseIpv4Loopback_WhenIpv6AnswersHttp521()
    {
        using var v4 = new TcpListener(IPAddress.Loopback, 0);
        v4.Start();
        var port = ((IPEndPoint)v4.LocalEndpoint).Port;
        TcpListener? v6 = null;
        try
        {
            v6 = new TcpListener(IPAddress.IPv6Loopback, port);
            v6.Start();
        }
        catch (SocketException)
        {
            v6 = null;
        }

        if (v6 is null)
            Assert.Ignore("IPv6 loopback is not available.");

        using var v6Listener = v6;
        var v4Hits = 0;
        var v6Hits = 0;
        var v4Accept = AcceptAsync(v4, 200, () => Interlocked.Increment(ref v4Hits));
        var v6Accept = AcceptAsync(v6Listener, 521, () => Interlocked.Increment(ref v6Hits));

        using var handler = K7ServerSockets.Create();
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri($"http://localhost:{port}/"),
            Timeout = TimeSpan.FromSeconds(8)
        };

        var response = await http.GetAsync("api/server-info");

        ((int)response.StatusCode).Should().Be(200);
        v4Hits.Should().BeGreaterThan(0);
        v6Hits.Should().Be(0);

        v4.Stop();
        v6Listener.Stop();
        await Task.WhenAll(v4Accept, v6Accept);
    }

    private static async Task AcceptAsync(TcpListener listener, int status, Action onHit)
    {
        try
        {
            while (true)
            {
                var socket = await listener.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        onHit();
                        var stream = socket.GetStream();
                        _ = await stream.ReadAsync(new byte[2048]);
                        var body = status == 200 ? """{"guestEnabled":false}""" : "origin down";
                        var payload =
                            $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Web Server Is Down")}\r\n" +
                            $"Content-Length: {Encoding.ASCII.GetByteCount(body)}\r\n" +
                            "Connection: close\r\n\r\n" +
                            body;
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(payload));
                    }
                    finally
                    {
                        socket.Dispose();
                    }
                });
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
    }
}

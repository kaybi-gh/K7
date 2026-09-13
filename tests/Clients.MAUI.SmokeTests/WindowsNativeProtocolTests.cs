using K7.Clients.MAUI.Platforms.Windows;

namespace K7.Clients.MAUI.SmokeTests;

[TestFixture]
public class WindowsNativeProtocolTests
{
    [Test]
    public void BuildOpenCommand_ShouldPassUriArgumentToCurrentExe()
    {
        WindowsNativeProtocol.BuildOpenCommand(@"C:\Apps\K7.exe")
            .Should().Be(@"""C:\Apps\K7.exe"" ""%1""");
    }

    [Test]
    public void IsK7Callback_ShouldAcceptCustomSchemeOnly()
    {
        WindowsProtocolActivation.IsK7Callback(new Uri("k7://callback/login?code=abc")).Should().BeTrue();
        WindowsProtocolActivation.IsK7Callback(new Uri("http://localhost:9/")).Should().BeFalse();
    }

    [Test]
    public void CallbackFile_ShouldRoundTripK7UriAndIgnoreOtherSchemes()
    {
        var previous = WindowsProtocolCallbackFile.FilePath;
        WindowsProtocolCallbackFile.FilePath = Path.Combine(
            Path.GetTempPath(),
            $"k7-protocol-callback-{Guid.NewGuid():N}.uri");

        try
        {
            WindowsProtocolCallbackFile.Write(new Uri("http://localhost/"));
            File.Exists(WindowsProtocolCallbackFile.FilePath).Should().BeFalse();

            var expected = new Uri("k7://callback/login?code=abc&state=x");
            WindowsProtocolCallbackFile.Write(expected);
            WindowsProtocolCallbackFile.TryReadAndDelete().Should().Be(expected);
            File.Exists(WindowsProtocolCallbackFile.FilePath).Should().BeFalse();
            WindowsProtocolCallbackFile.TryReadAndDelete().Should().BeNull();
        }
        finally
        {
            if (File.Exists(WindowsProtocolCallbackFile.FilePath))
                File.Delete(WindowsProtocolCallbackFile.FilePath);

            WindowsProtocolCallbackFile.FilePath = previous;
        }
    }
}

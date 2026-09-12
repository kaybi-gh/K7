using AwesomeAssertions;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MpcCommandLineTests
{
    [Test]
    public void ToStartMilliseconds_ShouldConvertResumeTimestamp()
    {
        MpcCommandLine.ToStartMilliseconds(4323).Should().Be(4_323_000);
        MpcCommandLine.ToStartMilliseconds(0).Should().Be(0);
        MpcCommandLine.ToStartMilliseconds(null).Should().Be(0);
    }

    [Test]
    public void Build_ShouldQuoteUrlAndAppendStart()
    {
        var args = MpcCommandLine.Build(
            "http://127.0.0.1/api/indexed-files/1/direct-stream?ephemeral_token=abc",
            "/fullscreen /close",
            4_323_000,
            13579);

        args.Should().StartWith("/webport 13579");
        args.Should().Contain("/fullscreen /close");
        args.Should().Contain("/start 4323000");
        args.Should().EndWith("\"http://127.0.0.1/api/indexed-files/1/direct-stream?ephemeral_token=abc\"");
    }

    [Test]
    public void Build_ShouldSkipWebPort_WhenExtraArgsAlreadyHaveIt()
    {
        var args = MpcCommandLine.Build("http://x/file", "/webport 20000", 0, 13579);

        args.Should().Contain("/webport 20000");
        args.Should().NotContain("/webport 13579");
    }

    [Test]
    public void Build_ShouldOmitWebPort_WhenInvalid()
    {
        var args = MpcCommandLine.Build("http://x/file", "/fullscreen", 0, 0);

        args.Should().NotContain("/webport");
    }

    [Test]
    public void Build_ShouldSkipStart_WhenExtraArgsAlreadyHaveIt()
    {
        var args = MpcCommandLine.Build("http://x/file", "/start 10", 5000);

        args.Should().NotContain("/start 5000");
        args.Should().Contain("/start 10");
    }

    [Test]
    public void Build_ShouldOmitStart_WhenZero()
    {
        var args = MpcCommandLine.Build("http://x/file", "/fullscreen", 0);

        args.Should().NotContain("/start");
    }
}

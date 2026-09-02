using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class ExoVideoBufferPolicyTests
{
    [TestCase(null, ExoVideoBufferSize.Auto)]
    [TestCase("", ExoVideoBufferSize.Auto)]
    [TestCase("auto", ExoVideoBufferSize.Auto)]
    [TestCase("DEFAULT", ExoVideoBufferSize.Default)]
    [TestCase("large", ExoVideoBufferSize.Large)]
    [TestCase("extralarge", ExoVideoBufferSize.ExtraLarge)]
    public void Parse_ShouldMapStoredValues(string? stored, ExoVideoBufferSize expected)
    {
        ExoVideoBufferPolicy.Parse(stored).Should().Be(expected);
    }

    [Test]
    public void Persist_ShouldRoundTrip()
    {
        foreach (var size in Enum.GetValues<ExoVideoBufferSize>())
            ExoVideoBufferPolicy.Parse(ExoVideoBufferPolicy.Persist(size)).Should().Be(size);
    }

    [Test]
    public void Resolve_ShouldUseDefaultOnTelevision_WhenAuto()
    {
        ExoVideoBufferPolicy.Resolve(ExoVideoBufferSize.Auto, isTelevision: true)
            .Should().Be(ExoVideoBufferSize.Default);
    }

    [Test]
    public void Resolve_ShouldUseDefaultOnPhone_WhenAuto()
    {
        ExoVideoBufferPolicy.Resolve(ExoVideoBufferSize.Auto, isTelevision: false)
            .Should().Be(ExoVideoBufferSize.Default);
    }

    [Test]
    public void Resolve_ShouldKeepExplicitSize()
    {
        ExoVideoBufferPolicy.Resolve(ExoVideoBufferSize.ExtraLarge, isTelevision: true)
            .Should().Be(ExoVideoBufferSize.ExtraLarge);
    }

    [Test]
    public void Persist_ShouldBeDefault_WhenAutoResolvedOnTelevision()
    {
        ExoVideoBufferPolicy.Persist(
                ExoVideoBufferPolicy.Resolve(ExoVideoBufferSize.Auto, isTelevision: true))
            .Should().Be(ExoVideoBufferPolicy.Default);
    }

    [TestCase("eac3", true)]
    [TestCase("ac3", true)]
    [TestCase("dts", true)]
    [TestCase("truehd", true)]
    [TestCase("aac", false)]
    [TestCase(null, false)]
    public void IsPassthrough_ShouldDetectBitstreamCodecs(string? codec, bool expected)
    {
        VideoAudioPassthroughCodecs.IsPassthrough(codec).Should().Be(expected);
    }
}

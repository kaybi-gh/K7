using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class HdmiAutoFrameRatePolicyTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("auto")]
    [TestCase("unknown")]
    public void TryParse_ShouldBeNull_WhenUnsetOrUnknown(string? stored)
    {
        HdmiAutoFrameRatePolicy.TryParse(stored).Should().BeNull();
    }

    [TestCase("disabled", HdmiAutoFrameRateMode.Disabled)]
    [TestCase("DISABLED", HdmiAutoFrameRateMode.Disabled)]
    [TestCase("device", HdmiAutoFrameRateMode.ScaleOnDevice)]
    [TestCase("tv", HdmiAutoFrameRateMode.ScaleOnTv)]
    public void TryParse_ShouldMapStoredValues(string stored, HdmiAutoFrameRateMode expected)
    {
        HdmiAutoFrameRatePolicy.TryParse(stored).Should().Be(expected);
    }

    [Test]
    public void Persist_ShouldRoundTrip()
    {
        foreach (var mode in Enum.GetValues<HdmiAutoFrameRateMode>())
            HdmiAutoFrameRatePolicy.TryParse(HdmiAutoFrameRatePolicy.Persist(mode)).Should().Be(mode);
    }

    [Test]
    public void DefaultForDevice_ShouldBeDisabled_WhenAmlogic()
    {
        HdmiAutoFrameRatePolicy
            .DefaultForDevice(isTelevision: true, "SEI Robotics", "Nokia Streaming Box 8000")
            .Should().Be(HdmiAutoFrameRateMode.Disabled);
        HdmiAutoFrameRatePolicy
            .DefaultForDevice(isTelevision: false, "SEI Robotics", "Nokia Streaming Box 8000")
            .Should().Be(HdmiAutoFrameRateMode.Disabled);
    }

    [Test]
    public void DefaultForDevice_ShouldBeScaleOnDevice_WhenOtherTelevision()
    {
        HdmiAutoFrameRatePolicy
            .DefaultForDevice(isTelevision: true, "NVIDIA", "SHIELD Android TV")
            .Should().Be(HdmiAutoFrameRateMode.ScaleOnDevice);
    }

    [Test]
    public void DefaultForDevice_ShouldBeDisabled_WhenPhone()
    {
        HdmiAutoFrameRatePolicy
            .DefaultForDevice(isTelevision: false, "Google", "Pixel 8")
            .Should().Be(HdmiAutoFrameRateMode.Disabled);
    }

    [Test]
    public void Resolve_ShouldKeepExplicitMode()
    {
        HdmiAutoFrameRatePolicy
            .Resolve("tv", isTelevision: true, "SEI Robotics", "Nokia Streaming Box 8000")
            .Should().Be(HdmiAutoFrameRateMode.ScaleOnTv);
    }

    [Test]
    public void Resolve_ShouldUseDeviceDefault_WhenUnset()
    {
        HdmiAutoFrameRatePolicy
            .Resolve("", isTelevision: true, "SEI Robotics", "Nokia Streaming Box 8000")
            .Should().Be(HdmiAutoFrameRateMode.Disabled);
        HdmiAutoFrameRatePolicy
            .Resolve(null, isTelevision: true, "NVIDIA", "SHIELD Android TV")
            .Should().Be(HdmiAutoFrameRateMode.ScaleOnDevice);
    }
}

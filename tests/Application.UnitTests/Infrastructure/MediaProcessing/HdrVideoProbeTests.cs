using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

public class HdrVideoProbeTests
{
    [TestCase("smpte2084")]
    [TestCase("SMPTE2084")]
    [TestCase("arib-std-b67")]
    [TestCase("smpte2094-40")]
    [TestCase("smpte2094-10")]
    public void IsHdrTransfer_ShouldReturnTrue_WhenTransferIsHdr(string transfer)
    {
        HdrVideoProbe.IsHdrTransfer(transfer).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("bt709")]
    [TestCase("bt2020-10")]
    [TestCase("unknown")]
    [TestCase("unspecified")]
    [TestCase("N/A")]
    public void IsHdrTransfer_ShouldReturnFalse_WhenTransferIsNotHdr(string? transfer)
    {
        // Empty/unknown transfer is the Alita-style Main 10 SDR case: tonemap must not run.
        HdrVideoProbe.IsHdrTransfer(transfer).Should().BeFalse();
    }
}

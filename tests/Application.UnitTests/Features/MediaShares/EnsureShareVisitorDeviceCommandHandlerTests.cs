using K7.Server.Application.Features.MediaShares.Commands.EnsureShareVisitorDevice;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.MediaFormats;

namespace K7.Server.Application.UnitTests.Features.MediaShares;

[TestFixture]
public class EnsureShareVisitorDeviceCommandHandlerTests
{
    private static EnsureShareVisitorDeviceCommand CreateCommand(
        string[] containers,
        string[] audioCodecs,
        string[] videoCodecs,
        params string[] tokens) => new()
        {
            ShareId = Guid.NewGuid(),
            IndexedFileId = Guid.NewGuid(),
            SupportedContainers = containers,
            SupportedAudioCodecs = audioCodecs,
            SupportedVideoCodecs = videoCodecs,
            CapabilityTokens = tokens
        };

    [Test]
    public void ResolveFormatIds_ShouldReturnNoCatalogFormat_WhenBrowserReportsNothing()
    {
        var ids = EnsureShareVisitorDeviceCommandHandler.ResolveFormatIds(CreateCommand([], [], []));

        ids.Should().BeEmpty();
    }

    [Test]
    public void ResolveFormatIds_ShouldOnlyKeepFormatsTheBrowserPlays()
    {
        var audioFormat = Constants.MediaFormats.OfType<AudioMediaFormat>().First();

        var ids = EnsureShareVisitorDeviceCommandHandler.ResolveFormatIds(
            CreateCommand([audioFormat.Container], [audioFormat.Codec], []));

        ids.Should().Contain(audioFormat.Id);
        ids.Should().OnlyContain(id => Constants.MediaFormats
            .OfType<AudioMediaFormat>()
            .Any(format => format.Id == id
                && string.Equals(format.Container, audioFormat.Container, StringComparison.OrdinalIgnoreCase)
                && string.Equals(format.Codec, audioFormat.Codec, StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public void ResolveFormatIds_ShouldKeepCapabilityTokens()
    {
        var ids = EnsureShareVisitorDeviceCommandHandler.ResolveFormatIds(
            CreateCommand([], [], [], "achannels:2"));

        ids.Should().ContainSingle().Which.Should().Be("achannels:2");
    }
}

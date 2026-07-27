using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MetadataPictureDisplayHelperTests
{
    [Test]
    public void SizeFor_ShouldReturnSmall_WhenThumb()
    {
        MetadataPictureDisplayHelper.SizeFor(ImageDisplayRole.Thumb)
            .Should().Be(MetadataPictureSize.Small);
    }

    [Test]
    public void SizeFor_ShouldReturnMedium_WhenCard()
    {
        MetadataPictureDisplayHelper.SizeFor(ImageDisplayRole.Card)
            .Should().Be(MetadataPictureSize.Medium);
    }

    [Test]
    public void SizeFor_ShouldReturnNull_WhenHero()
    {
        MetadataPictureDisplayHelper.SizeFor(ImageDisplayRole.Hero)
            .Should().BeNull();
    }

    [Test]
    public void SizeForHeroBackdrop_ShouldReturnMedium()
    {
        MetadataPictureDisplayHelper.SizeForHeroBackdrop()
            .Should().Be(MetadataPictureSize.Medium);
    }
}

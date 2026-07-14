using K7.Server.Application.Common.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MetadataPictureVariantRulesTests
{
    [Test]
    public void IsPermanentVariantFallback_ShouldReturnTrue_WhenOriginalIsSmallerThanTarget()
    {
        MetadataPictureVariantRules.IsPermanentVariantFallback(
                MetadataPictureType.Still,
                MetadataPictureSize.Medium,
                640)
            .Should().BeTrue();
    }

    [Test]
    public void IsPermanentVariantFallback_ShouldReturnFalse_WhenOriginalCanProduceVariant()
    {
        MetadataPictureVariantRules.IsPermanentVariantFallback(
                MetadataPictureType.Still,
                MetadataPictureSize.Medium,
                1920)
            .Should().BeFalse();
    }

    [Test]
    public void IsPermanentVariantFallback_ShouldReturnFalse_WhenOriginalWidthIsUnknown()
    {
        MetadataPictureVariantRules.IsPermanentVariantFallback(
                MetadataPictureType.Still,
                MetadataPictureSize.Medium,
                null)
            .Should().BeFalse();
    }
}

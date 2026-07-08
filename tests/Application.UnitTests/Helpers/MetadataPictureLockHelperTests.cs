using FluentAssertions;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

public class MetadataPictureLockHelperTests
{
    [Test]
    public void IsPictureTypeLocked_ShouldBeTrue_WhenAllPicturesLocked()
    {
        var lockedFields = new List<string> { MetadataPictureLockHelper.AllPicturesField };

        MetadataPictureLockHelper.IsPictureTypeLocked(lockedFields, MetadataPictureType.Still)
            .Should().BeTrue();
    }

    [Test]
    public void IsPictureTypeLocked_ShouldBeTrue_WhenSpecificTypeLocked()
    {
        var lockedFields = new List<string> { MetadataPictureLockHelper.GetTypeField(MetadataPictureType.Poster) };

        MetadataPictureLockHelper.IsPictureTypeLocked(lockedFields, MetadataPictureType.Poster)
            .Should().BeTrue();
        MetadataPictureLockHelper.IsPictureTypeLocked(lockedFields, MetadataPictureType.Backdrop)
            .Should().BeFalse();
    }

    [Test]
    public void IsPictureTypeLocked_ShouldPreferAllPictures_WhenBothArePresent()
    {
        var lockedFields = new List<string>
        {
            MetadataPictureLockHelper.AllPicturesField,
            MetadataPictureLockHelper.GetTypeField(MetadataPictureType.Still)
        };

        MetadataPictureLockHelper.IsPictureTypeLocked(lockedFields, MetadataPictureType.Backdrop)
            .Should().BeTrue();
    }
}

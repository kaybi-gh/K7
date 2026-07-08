using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Mappings;

[TestFixture]
public class MetadataPictureMappingsTests
{
    [Test]
    public void ToMetadataPictureDto_ShouldOmitUri_WhenLocalPathIsNull()
    {
        var picture = new MetadataPicture
        {
            Id = Guid.NewGuid(),
            Type = MetadataPictureType.Poster,
            LocalPath = null
        };

        var dto = picture.ToMetadataPictureDto();

        dto.Uri.Should().BeNull();
        dto.Id.Should().Be(picture.Id);
        dto.Type.Should().Be(MetadataPictureType.Poster);
    }

    [Test]
    public void ToMetadataPictureDto_ShouldExposeUri_WhenLocalPathIsSet()
    {
        var pictureId = Guid.NewGuid();
        var picture = new MetadataPicture
        {
            Id = pictureId,
            Type = MetadataPictureType.Logo,
            LocalPath = "/data/metadatas/medias/logo.webp"
        };

        var dto = picture.ToMetadataPictureDto();

        dto.Uri.Should().NotBeNull();
        dto.Uri!.OriginalString.Should().Be($"/api/metadata-pictures/{pictureId}");
    }
}

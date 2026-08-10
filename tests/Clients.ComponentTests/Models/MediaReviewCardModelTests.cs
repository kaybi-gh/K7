using K7.Clients.Shared.UI.Models;
using K7.Shared.Dtos.Entities.Reviews;

namespace K7.Clients.ComponentTests.Models;

[TestFixture]
public class MediaReviewCardModelTests
{
    [Test]
    public void FromLocal_ShouldMapAvatarUrl_WhenAvatarPictureIdPresent()
    {
        var pictureId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var review = new MediaReviewDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            UserRatingId = Guid.NewGuid(),
            Text = "Great",
            Rating = 8,
            UserDisplayName = "Kaybi",
            AvatarPictureId = pictureId,
            Created = DateTimeOffset.UtcNow
        };

        var model = MediaReviewCardModel.FromLocal(review);

        model.AvatarUrl.Should().Be($"/api/metadata-pictures/{pictureId}?size=Medium");
        model.DisplayName.Should().Be("Kaybi");
    }

    [Test]
    public void FromLocal_ShouldLeaveAvatarUrlNull_WhenAvatarPictureIdMissing()
    {
        var review = new MediaReviewDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MediaId = Guid.NewGuid(),
            UserRatingId = Guid.NewGuid(),
            Text = "Great",
            Rating = 8,
            UserDisplayName = "Kaybi",
            Created = DateTimeOffset.UtcNow
        };

        MediaReviewCardModel.FromLocal(review).AvatarUrl.Should().BeNull();
    }
}

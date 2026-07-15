using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Helpers;

namespace K7.Server.Domain.UnitTests.Entities.Medias;

public class BaseMediaFieldLockTests
{
    [Test]
    public void LockField_ShouldBeIdempotent()
    {
        var movie = new Movie { Title = "A" };

        movie.LockField(nameof(Movie.Title));
        movie.LockField(nameof(Movie.Title));

        movie.LockedFields.Should().Equal(nameof(Movie.Title));
        movie.IsFieldLocked(nameof(Movie.Title)).Should().BeTrue();
    }

    [Test]
    public void UnlockField_ShouldRemoveLock()
    {
        var movie = new Movie { Title = "A" };
        movie.LockField(nameof(Movie.Title));

        movie.UnlockField(nameof(Movie.Title));

        movie.IsFieldLocked(nameof(Movie.Title)).Should().BeFalse();
    }

    [Test]
    public void RemovePicturesOfType_ShouldOnlyRemoveMatchingType()
    {
        var movie = new Movie { Title = "A" };
        movie.Pictures.Add(new MetadataPicture { Type = MetadataPictureType.Poster, LocalPath = "p" });
        movie.Pictures.Add(new MetadataPicture { Type = MetadataPictureType.Logo, LocalPath = "l" });

        movie.RemovePicturesOfType(MetadataPictureType.Poster);

        movie.Pictures.Should().ContainSingle(p => p.Type == MetadataPictureType.Logo);
    }

    [Test]
    public void IsPictureTypeLocked_ShouldHonorAllPicturesLock()
    {
        var movie = new Movie { Title = "A" };
        movie.LockedFields.Add(MetadataPictureLockHelper.AllPicturesField);

        movie.IsPictureTypeLocked(MetadataPictureType.Cover).Should().BeTrue();
    }
}

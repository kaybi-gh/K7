using K7.Server.Domain.Constants;

namespace K7.Server.Application.UnitTests.Services;

public class ConcurrencyGroupsTests
{
    [Test]
    public void Tmdb_ShouldBeLowercase()
    {
        ConcurrencyGroups.Tmdb.Should().Be("tmdb");
    }

    [Test]
    public void MusicBrainz_ShouldBeLowercase()
    {
        ConcurrencyGroups.MusicBrainz.Should().Be("musicbrainz");
    }

    [Test]
    public void Ffmpeg_ShouldBeLowercase()
    {
        ConcurrencyGroups.Ffmpeg.Should().Be("ffmpeg");
    }

    [Test]
    public void ImageProcessing_ShouldBeKebabCase()
    {
        ConcurrencyGroups.ImageProcessing.Should().Be("image-processing");
    }

    [Test]
    public void AllGroups_ShouldBeDistinct()
    {
        var groups = new[]
        {
            ConcurrencyGroups.Tmdb,
            ConcurrencyGroups.MusicBrainz,
            ConcurrencyGroups.Ffmpeg,
            ConcurrencyGroups.ImageProcessing
        };

        groups.Should().OnlyHaveUniqueItems();
    }
}

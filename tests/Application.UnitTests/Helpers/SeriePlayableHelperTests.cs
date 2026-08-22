using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class SeriePlayableHelperTests
{
    [Test]
    public void HasPlayableFile_ShouldReturnTrue_WhenLocalIndexedFileExists()
    {
        var episode = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            IndexedFiles = [new IndexedFile { Id = Guid.NewGuid(), Name = "e.mkv", Extension = ".mkv", Path = "/e.mkv", Hash = 1, Size = 1, LibraryId = Guid.NewGuid() }]
        };

        SeriePlayableHelper.HasPlayableFile(episode).Should().BeTrue();
    }

    [Test]
    public void HasPlayableFile_ShouldReturnFalse_WhenNoFiles()
    {
        var episode = new SerieEpisode { Id = Guid.NewGuid(), IndexedFiles = [], RemoteIndexedFiles = [] };

        SeriePlayableHelper.HasPlayableFile(episode).Should().BeFalse();
    }

    [Test]
    public void CountPlayableEpisodes_ShouldCountOnlyEpisodesWithFiles()
    {
        var playable = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            IndexedFiles = [new IndexedFile { Id = Guid.NewGuid(), Name = "e.mkv", Extension = ".mkv", Path = "/e.mkv", Hash = 1, Size = 1, LibraryId = Guid.NewGuid() }]
        };
        var orphan = new SerieEpisode { Id = Guid.NewGuid(), IndexedFiles = [], RemoteIndexedFiles = [] };

        SeriePlayableHelper.CountPlayableEpisodes([playable, orphan]).Should().Be(1);
    }
}

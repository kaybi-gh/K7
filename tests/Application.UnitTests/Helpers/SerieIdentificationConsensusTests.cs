using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Models;

namespace K7.Server.Application.UnitTests.Helpers;

public class SerieIdentificationConsensusTests
{
    [Test]
    public void ResolveCanonicalSeriesTitle_ShouldUnify_WhenTitlesAreCloseVariants()
    {
        var canonical = SerieIdentificationConsensus.ResolveCanonicalSeriesTitle(
        [
            "Frieren Beyond Journeys End",
            "Frieren Beyond Journey's End",
            "Frieren Beyond Journeys End"
        ]);

        canonical.Should().Be("Frieren Beyond Journeys End");
    }

    [Test]
    public void ResolveCanonicalSeriesTitle_ShouldPreferLongerTitle_WhenFrequencyTied()
    {
        SerieIdentificationConsensus.AreSeriesTitlesClose(
            "Attack on Titan",
            "Attack on Titan The Final Season").Should().BeTrue();

        var canonical = SerieIdentificationConsensus.ResolveCanonicalSeriesTitle(
        [
            "Attack on Titan",
            "Attack on Titan The Final Season"
        ]);

        canonical.Should().Be("Attack on Titan The Final Season");
    }

    [Test]
    public void ResolveCanonicalSeriesTitle_ShouldReturnNull_WhenFolderIsAmbiguous()
    {
        var canonical = SerieIdentificationConsensus.ResolveCanonicalSeriesTitle(
        [
            "Show Alpha",
            "Show Beta",
            "Show Gamma"
        ]);

        canonical.Should().BeNull();
    }

    [Test]
    public void ResolveCanonicalSeriesTitle_ShouldUseDominantCluster_WhenMinorityOutlierExists()
    {
        var canonical = SerieIdentificationConsensus.ResolveCanonicalSeriesTitle(
        [
            "Cool Show",
            "Cool Show",
            "Cool Show",
            "Cool Show",
            "Completely Different"
        ]);

        canonical.Should().Be("Cool Show");
    }

    [Test]
    public void ApplyDirectoryTitleConsensus_ShouldRewriteSiblingTitles()
    {
        var files = new List<IndexedFile>
        {
            CreateFile("Show Name S01E01.mkv", "Show Name"),
            CreateFile("Show Name S01E02.mkv", "Show Nam"),
            CreateFile("Show Name S01E03.mkv", "Show Name")
        };

        SerieIdentificationConsensus.ApplyDirectoryTitleConsensus(files);

        files.Select(f => f.Identification!.SeriesTitle).Should().OnlyContain(t => t == "Show Name");
    }

    [Test]
    public void ApplyDirectoryTitleConsensus_ShouldLeaveTitles_WhenSingleFile()
    {
        var files = new List<IndexedFile>
        {
            CreateFile("Solo Show S01E01.mkv", "Solo Show")
        };

        SerieIdentificationConsensus.ApplyDirectoryTitleConsensus(files);

        files[0].Identification!.SeriesTitle.Should().Be("Solo Show");
    }

    [Test]
    public void AreSeriesTitlesClose_ShouldIgnoreCaseAndWhitespace()
    {
        SerieIdentificationConsensus.AreSeriesTitlesClose("My  Show", "my show").Should().BeTrue();
    }

    [Test]
    public void AreSeriesTitlesClose_ShouldReturnFalse_WhenTitlesAreUnrelated()
    {
        SerieIdentificationConsensus.AreSeriesTitlesClose("Alpha", "Beta").Should().BeFalse();
    }

    [Test]
    public void EpisodeIdentityEquals_ShouldBeTrue_WhenOnlyEpisodeTitleInFilenameWouldDiffer()
    {
        var left = new MediaIdentification("Show")
        {
            SeriesTitle = "Show",
            SeasonNumber = 0,
            EpisodeNumber = 26
        };
        var right = new MediaIdentification("Show")
        {
            SeriesTitle = "Show",
            SeasonNumber = 0,
            EpisodeNumber = 26
        };

        SerieIdentificationConsensus.EpisodeIdentityEquals(left, right).Should().BeTrue();
    }

    [Test]
    public void EpisodeIdentityEquals_ShouldBeFalse_WhenSeasonOrEpisodeChanges()
    {
        var left = new MediaIdentification("Show")
        {
            SeriesTitle = "Show",
            SeasonNumber = 0,
            EpisodeNumber = 99
        };
        var right = new MediaIdentification("Show")
        {
            SeriesTitle = "Show",
            SeasonNumber = 0,
            EpisodeNumber = 26
        };

        SerieIdentificationConsensus.EpisodeIdentityEquals(left, right).Should().BeFalse();
    }

    [Test]
    public void EpisodeIdentityEquals_ShouldBeFalse_WhenSeriesTitleChanges()
    {
        var left = new MediaIdentification("Show A")
        {
            SeriesTitle = "Show A",
            SeasonNumber = 1,
            EpisodeNumber = 1
        };
        var right = new MediaIdentification("Show B")
        {
            SeriesTitle = "Show B",
            SeasonNumber = 1,
            EpisodeNumber = 1
        };

        SerieIdentificationConsensus.EpisodeIdentityEquals(left, right).Should().BeFalse();
    }

    private static IndexedFile CreateFile(string name, string seriesTitle) =>
        new()
        {
            Id = Guid.NewGuid(),
            LibraryId = Guid.NewGuid(),
            Name = name,
            Extension = ".mkv",
            Path = $"/library/{name}",
            ParentDirectory = "/library",
            Hash = 1,
            Size = 1,
            Identification = new MediaIdentification(seriesTitle)
            {
                SeriesTitle = seriesTitle,
                SeasonNumber = 1,
                EpisodeNumber = 1
            }
        };
}

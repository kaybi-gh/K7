using K7.Server.Application.Helpers;

namespace Application.UnitTests.Helpers;

[TestFixture]
public class ThemeSongLocatorTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "k7-theme-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void FindLibrarySidecar_ShouldPreferThemeMp3()
    {
        var themePath = Path.Combine(_tempDir, "theme.mp3");
        File.WriteAllText(themePath, "x");

        var found = ThemeSongLocator.FindLibrarySidecar(_tempDir, Path.Combine(_tempDir, "Movie.mkv"));

        found.Should().Be(themePath);
    }

    [Test]
    public void FindLibrarySidecar_ShouldUseSameStemAudio_WhenNoThemeFile()
    {
        var videoPath = Path.Combine(_tempDir, "Movie Name (2020).mkv");
        var audioPath = Path.Combine(_tempDir, "Movie Name (2020).flac");
        File.WriteAllText(audioPath, "x");

        var found = ThemeSongLocator.FindLibrarySidecar(_tempDir, videoPath);

        found.Should().Be(audioPath);
    }

    [Test]
    public void ResolveSerieRootFromEpisodePath_ShouldReturnParentOfSeasonFolder()
    {
        var episodePath = Path.Combine(_tempDir, "Show", "Season 01", "ep.mkv");

        var root = ThemeSongLocator.ResolveSerieRootFromEpisodePath(episodePath);

        root.Should().Be(Path.Combine(_tempDir, "Show"));
    }
}

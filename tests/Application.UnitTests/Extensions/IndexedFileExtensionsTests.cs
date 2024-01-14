using System.Security.Cryptography;
using System.Text;
using MediaServer.Application.Extensions;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;
using MediaServer.Tests.Helpers.Fixtures;
using MediaServer.Tests.Helpers.Helpers;

namespace MediaServer.Application.UnitTests.Extensions;

public class IndexedFileExtensionsTests : FileFixture
{
    [TestCase("", "1884 (1995).mp4", "1884", 1995)]
    [TestCase("", "1884 - 1995.mp4", "1884", 1995)]
    [TestCase("", "Movie (1990).mp4", "Movie", 1990)]
    [TestCase("", "Movie - 1990.mp4", "Movie", 1990)]
    [TestCase("", "Movie 1884 (1995).mp4", "Movie 1884", 1995)]
    [TestCase("", "Movie 1990.mp4", "Movie", 1990)]
    [TestCase("", "Movie(1990).mp4", "Movie", 1990)]
    [TestCase("", "Movie.1990.noise.mp4", "Movie", 1990)]
    [TestCase("", "Movie.noise.1990.mp4", "Movie.noise", 1990)]
    [TestCase("", "Movie.noise.1990.noise.mp4", "Movie.noise", 1990)]
    [TestCase("", "The incredible movie (1995).mp4", "The incredible movie", 1995)]
    [TestCase("AnotherMovie", "Movie - 1990.mp4", "Movie", 1990)]
    [TestCase("AnotherMovie", "Movie - 1990.mp4", "Movie", 1990)]
    [TestCase("Movie (2000)", "Movie (2001).mkv", "Movie", 2001)]
    [TestCase("Movie (2000)", "Movie.mkv", "Movie", 2000)]
    [TestCase("Movie (2000)", "RandomFileTitle.mkv", "Movie", 2000)]
    [TestCase("Movie (2005)", "Movie cd1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie part1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie-cd1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie-part1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie.cd1.avi", "Movie", 2005)]
    [TestCase("Movie (2005)", "Movie.part1.avi", "Movie", 2005)]
    [TestCase(null, "Movie(1990).mp4", "Movie", 1990)]
    public void ShouldParseMovieFileCorrectly(string? parentDirectory, string fileName, string expectedMovieTitle, int expectedReleaseYear)
    {
        // Arrange
        var path = string.IsNullOrEmpty(parentDirectory) ? fileName : Path.Combine(parentDirectory, fileName);
        var fileInfo = FileHelper.CreateTestFile(path, "content");
        var library = new Library()
        {
            Id = 1,
            MediaType = LibraryMediaType.Movie,
            RootPath = FileHelper.TestDirectoryPath,
            Title = "Movies"
        };
        var indexedFile = fileInfo.ToIndexedFile(library.Id);

        // Act
        indexedFile!.TryIdentifyMovie(library, out Movie? movie);

        // Assert
        //indexedFile!.IsIdentified.Should().BeTrue();
        movie!.Title.Should().Be(expectedMovieTitle);
        movie!.ReleaseYear!.Value.Year.Should().Be(expectedReleaseYear);
    }
}

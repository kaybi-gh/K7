using K7.Server.Application.Features.Search.Queries.GlobalSearch;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Tests.Helpers.Fixtures;

namespace K7.Server.Application.FunctionalTests.Search.Queries;

public class GlobalSearchTests : DatabaseFixture
{
    [Test]
    public async Task ShouldReturnMovie_WhenTitleMatches()
    {
        // Arrange
        await RunAsAdministratorAsync();
        await AddAsync(CreateMovie("Inception"));
        await AddAsync(CreateMovie("Interstellar"));

        // Act
        var result = await SendAsync(new GlobalSearchQuery { Q = "Inception" });

        // Assert
        result.MediaResults.Should().ContainSingle(m => m.Title == "Inception");
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenQueryTooShort()
    {
        // Arrange
        await RunAsAdministratorAsync();
        await AddAsync(CreateMovie("Inception"));

        // Act
        var result = await SendAsync(new GlobalSearchQuery { Q = "a" });

        // Assert
        result.MediaResults.Should().BeEmpty();
    }

    [Test]
    public async Task ShouldDenyAnonymousUser()
    {
        // Arrange
        var query = new GlobalSearchQuery { Q = "Inception" };

        // Act
        var action = () => SendAsync(query);

        // Assert
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static Movie CreateMovie(string title)
    {
        var movie = new Movie { Title = title };
        movie.IndexedFiles.Add(new IndexedFile
        {
            LibraryId = Guid.NewGuid(),
            Name = title,
            Extension = ".mkv",
            Path = $"/movies/{title}.mkv",
            Hash = (uint)title.GetHashCode(),
            Size = 1024
        });

        return movie;
    }
}

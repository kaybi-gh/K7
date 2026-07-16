using K7.Server.Application.Features.Search.Queries.GlobalSearch;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Tests.Helpers.Fixtures;

namespace K7.Server.Application.FunctionalTests.Search.Queries;

public class GlobalSearchTests : DatabaseFixture
{
    [Test]
    public async Task ShouldReturnMovie_WhenTitleMatches()
    {
        await RunAsAdministratorAsync();
        var libraryId = await SeedLibraryAsync();
        await AddAsync(CreateMovie("Inception", libraryId));
        await AddAsync(CreateMovie("Interstellar", libraryId));

        var result = await SendAsync(new GlobalSearchQuery { Q = "Inception" });

        result.MediaResults.Should().ContainSingle(m => m.Title == "Inception");
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenQueryTooShort()
    {
        await RunAsAdministratorAsync();
        var libraryId = await SeedLibraryAsync();
        await AddAsync(CreateMovie("Inception", libraryId));

        var result = await SendAsync(new GlobalSearchQuery { Q = "a" });

        result.MediaResults.Should().BeEmpty();
    }

    private static async Task<Guid> SeedLibraryAsync()
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        await AddAsync(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        await AddAsync(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/movies",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        return libraryId;
    }

    private static Movie CreateMovie(string title, Guid libraryId)
    {
        var movie = new Movie { Title = title };
        movie.IndexedFiles.Add(new IndexedFile
        {
            LibraryId = libraryId,
            Name = title,
            Extension = ".mkv",
            Path = $"/movies/{title}.mkv",
            Hash = (uint)title.GetHashCode(),
            Size = 1024
        });
        return movie;
    }
}

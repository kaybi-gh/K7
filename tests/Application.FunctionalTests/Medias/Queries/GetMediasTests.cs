using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Medias.Queries.GetMedias;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Tests.Helpers.Fixtures;

namespace K7.Server.Application.FunctionalTests.Medias.Queries;

public class GetMediasTests : DatabaseFixture
{
    [Test]
    public async Task ShouldReturnMovies()
    {
        // Arrange
        await RunAsDefaultUserAsync();
        await AddAsync(CreateMovie("Inception"));
        await AddAsync(CreateMovie("Interstellar"));

        // Act
        var result = await SendAsync(new GetMediasWithPaginationQuery
        {
            PageNumber = 1,
            PageSize = 10
        });

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Should().Contain(m => m.Title == "Inception");
        result.Items.Should().Contain(m => m.Title == "Interstellar");
    }

    [Test]
    public async Task ShouldFilterByMediaType()
    {
        // Arrange
        await RunAsDefaultUserAsync();
        await AddAsync(CreateMovie("Inception"));

        // Act
        var result = await SendAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = new EnumHashSetQueryParam<MediaType> { MediaType.MusicTrack },
            PageNumber = 1,
            PageSize = 10
        });

        // Assert
        result.TotalCount.Should().Be(0);
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

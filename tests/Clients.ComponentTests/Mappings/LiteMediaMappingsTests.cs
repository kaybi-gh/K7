using K7.Clients.Shared.Mappings;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Home;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Mappings;

[TestFixture]
public class LiteMediaMappingsTests
{
    [Test]
    public void ToCardViewModel_ShouldUseReleaseYear_WhenAdditionalInfoIsNull()
    {
        // Arrange
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Inception",
            MediaType = MediaType.Movie,
            NavigationTarget = "/movies/1",
            AdditionalInfo = null,
            ReleaseDate = new DateOnly(2010, 7, 16)
        };
        var apiClient = Substitute.For<IK7ServerService>();

        // Act
        var result = item.ToCardViewModel(apiClient);

        // Assert
        result.AdditionalInformations.Should().Be("2010");
    }

    [Test]
    public void ToCardViewModel_ShouldPreferAdditionalInfo_WhenPresent()
    {
        // Arrange
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Inception",
            MediaType = MediaType.Movie,
            NavigationTarget = "/movies/1",
            AdditionalInfo = "2h 28m left",
            ReleaseDate = new DateOnly(2010, 7, 16)
        };
        var apiClient = Substitute.For<IK7ServerService>();

        // Act
        var result = item.ToCardViewModel(apiClient);

        // Assert
        result.AdditionalInformations.Should().Be("2h 28m left");
    }

    [Test]
    public void ToCardViewModel_ShouldUseSeriePoster_WhenEpisodeHasNoStill()
    {
        var posterUri = new Uri("/api/pictures/poster.jpg", UriKind.Relative);
        var item = new HomeFeedItemDto
        {
            Id = Guid.NewGuid(),
            Title = "Breaking Bad",
            MediaType = MediaType.SerieEpisode,
            NavigationTarget = "/series/1/seasons/1#ep-2",
            AdditionalInfo = "S01E02",
            Pictures =
            [
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Poster,
                    Uri = posterUri
                }
            ]
        };
        var apiClient = Substitute.For<IK7ServerService>();
        apiClient.GetAbsoluteUri(Arg.Any<string?>()).Returns(call =>
            call.Arg<string?>() is null ? null : new Uri($"https://localhost{call.Arg<string?>()}", UriKind.Absolute));

        var result = item.ToCardViewModel(apiClient);

        result.PictureUrl.Should().Be("https://localhost/api/pictures/poster.jpg?size=Small");
    }
}

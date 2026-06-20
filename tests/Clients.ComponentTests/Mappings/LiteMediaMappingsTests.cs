using K7.Clients.Shared.Mappings;
using K7.Server.Domain.Enums;
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
}

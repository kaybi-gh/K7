using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.UnitTests.Features.Medias.Queries;

[TestFixture]
public class MediaOrderingHelperTests
{
    [TestCase("TMDb", MetadataProvider.TMDb)]
    [TestCase("tmdb", MetadataProvider.TMDb)]
    [TestCase("Local", MetadataProvider.Local)]
    public void TryParseMetadataProvider_ShouldParseKnownProviders(string input, MetadataProvider expected)
    {
        var result = MediaOrderingHelper.TryParseMetadataProvider(input);
        result.Should().Be(expected);
    }

    [Test]
    public void TryParseMetadataProvider_ShouldReturnNull_ForUnknownProvider()
    {
        MediaOrderingHelper.TryParseMetadataProvider("federation").Should().BeNull();
    }

    [Test]
    public void RequiresUserPlayCount_ShouldBeTrue_WhenUserPlayCountOrderingPresent()
    {
        HashSet<GenreOrderingOption> orderBy = [GenreOrderingOption.UserPlayCountDesc, GenreOrderingOption.MediaCountDesc];
        MediaOrderingHelper.RequiresUserPlayCount(orderBy).Should().BeTrue();
    }

    [Test]
    public void RequiresUserPlayCount_ShouldBeFalse_WhenOnlyCatalogOrdering()
    {
        HashSet<GenreOrderingOption> orderBy = [GenreOrderingOption.MediaCountDesc];
        MediaOrderingHelper.RequiresUserPlayCount(orderBy).Should().BeFalse();
    }
}

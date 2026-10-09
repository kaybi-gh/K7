using K7.Server.Application.Common.Services;

namespace K7.Server.Application.UnitTests.Common.Services;

public class MediaTextSearchHelperTests
{
    [Test]
    public void BuildSortTitlePattern_ShouldStripDiacritics()
    {
        MediaTextSearchHelper.BuildSortTitlePattern("Élément", supportsTrigramSearch: true)
            .Should().Be("%element%");
    }

    [Test]
    public void BuildTitlePattern_ShouldKeepDiacritics()
    {
        MediaTextSearchHelper.BuildTitlePattern("Élément", supportsTrigramSearch: true)
            .Should().Be("%élément%");
    }
}

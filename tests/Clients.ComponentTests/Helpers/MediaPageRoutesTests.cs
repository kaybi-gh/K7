using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MediaPageRoutesTests
{
    [TestCase("movies/42", MediaPageKind.Movie)]
    [TestCase("/Movies/42/", MediaPageKind.Movie)]
    [TestCase("series/42", MediaPageKind.Series)]
    [TestCase("series/42/seasons/1", MediaPageKind.Season)]
    [TestCase("series/42/seasons/1/episodes/3", MediaPageKind.Episode)]
    [TestCase("persons/42", MediaPageKind.Person)]
    public void KindOf_ShouldTellTheKindOfAMediaPage(string path, MediaPageKind expected)
    {
        MediaPageRoutes.KindOf(path).Should().Be(expected);
    }

    [TestCase("")]
    [TestCase("movies")]
    [TestCase("series/42/seasons")]
    [TestCase("settings/interface/media-pages")]
    [TestCase("movies/42/extra")]
    public void KindOf_ShouldBeNull_ForAnyOtherPage(string path)
    {
        MediaPageRoutes.KindOf(path).Should().BeNull();
    }
}

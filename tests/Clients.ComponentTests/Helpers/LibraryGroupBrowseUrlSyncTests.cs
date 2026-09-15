using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

public class LibraryGroupBrowseUrlSyncTests
{
    [Test]
    public void MergeBrowseHref_ShouldClearFilter_WhenTargetHasNoQuery()
    {
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var current = new Uri($"https://k7.local/library-groups/{groupId}?filter=abc&sort=titleDesc");
        var target = new Uri($"https://k7.local/library-groups/{groupId}");

        LibraryGroupBrowseUrlSync.MergeBrowseHref(current, target)
            .Should().Be($"/library-groups/{groupId}");
    }

    [Test]
    public void MergeBrowseHref_ShouldReplaceFilter_WhenTargetHasAnotherFilter()
    {
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var current = new Uri($"https://k7.local/library-groups/{groupId}?filter=abc");
        var target = new Uri($"https://k7.local/library-groups/{groupId}?filter=xyz");

        LibraryGroupBrowseUrlSync.MergeBrowseHref(current, target)
            .Should().Be($"/library-groups/{groupId}?filter=xyz");
    }

    [Test]
    public void MergeBrowseHref_ShouldKeepTarget_WhenPathChanges()
    {
        var current = new Uri("https://k7.local/library-groups/11111111-1111-1111-1111-111111111111?filter=abc");
        var target = new Uri("https://k7.local/search");

        LibraryGroupBrowseUrlSync.MergeBrowseHref(current, target)
            .Should().Be("/search");
    }

    [Test]
    public void Fingerprint_ShouldIgnoreDefaultSort_WhenFilterIsEmpty()
    {
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var empty = LibraryGroupBrowseNavigationHelper.ParseBrowseState(new Dictionary<string, string>());

        LibraryGroupBrowseUrlSync.Fingerprint(groupId, empty)
            .Should().Be($"/library-groups/{groupId}");
    }
}

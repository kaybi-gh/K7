using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class UnloadedBrowseItemTests
{
    [Test]
    public void IdFor_ShouldBeStable_ForTheSameSlot()
    {
        UnloadedBrowseItem.IdFor(52).Should().Be(UnloadedBrowseItem.IdFor(52));
    }

    [Test]
    public void IdFor_ShouldDiffer_ForDifferentSlots()
    {
        UnloadedBrowseItem.IdFor(0).Should().NotBe(UnloadedBrowseItem.IdFor(1));
    }
}

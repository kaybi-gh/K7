using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MiniPlayerSlotsTests
{
    [Test]
    public void Queries_ShouldSplitByWidth_ByDefault()
    {
        MiniPlayerSlots.BarQuery(MiniPlayerPlacement.Auto).Should().Be("(min-width: 600px)");
        MiniPlayerSlots.DockQuery(MiniPlayerPlacement.Auto).Should().Be("(max-width: 599.98px)");
    }

    [Test]
    public void Queries_ShouldGiveEveryWidthToTheDock_WhenThePlayerIsAtTheBottom()
    {
        MiniPlayerSlots.BarQuery(MiniPlayerPlacement.Bottom).Should().Be("not all");
        MiniPlayerSlots.DockQuery(MiniPlayerPlacement.Bottom).Should().Be("all");
    }
}

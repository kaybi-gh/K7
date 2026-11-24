using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class ProfilePickerOrderTests
{
    [Test]
    public void Arrange_ShouldKeepSourceOrder_WhenNothingWasSaved()
    {
        var items = new[] { "a", "b", "c" };

        var ordered = ProfilePickerOrder.Arrange(items, item => item, []);

        ordered.Should().Equal("a", "b", "c");
    }

    [Test]
    public void Arrange_ShouldApplySavedOrder_AndAppendUnknownItems()
    {
        var items = new[] { "user:a", "group:b", "user:c" };
        var saved = new[] { "user:c", "user:a" };

        var ordered = ProfilePickerOrder.Arrange(items, item => item, saved);

        ordered.Should().Equal("user:c", "user:a", "group:b");
    }
}

using AwesomeAssertions;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class NativeSettingsFocusNavigatorTests
{
    [Test]
    public void MoveFocus_ShouldReturnNegativeOne_WhenListEmpty()
    {
        NativeSettingsFocusNavigator.MoveFocus(-1, 0, 1).Should().Be(-1);
    }

    [Test]
    public void MoveFocus_ShouldStartAtFirstRow_WhenNoCurrentFocusAndMovingDown()
    {
        NativeSettingsFocusNavigator.MoveFocus(-1, 5, 1).Should().Be(0);
    }

    [Test]
    public void MoveFocus_ShouldStartAtLastRow_WhenNoCurrentFocusAndMovingUp()
    {
        NativeSettingsFocusNavigator.MoveFocus(-1, 5, -1).Should().Be(4);
    }

    [Test]
    public void MoveFocus_ShouldAdvance_WhenMovingDown()
    {
        NativeSettingsFocusNavigator.MoveFocus(1, 5, 1).Should().Be(2);
    }

    [Test]
    public void MoveFocus_ShouldRetreat_WhenMovingUp()
    {
        NativeSettingsFocusNavigator.MoveFocus(1, 5, -1).Should().Be(0);
    }

    [Test]
    public void MoveFocus_ShouldClampAtBottom_WhenAlreadyOnLastRow()
    {
        NativeSettingsFocusNavigator.MoveFocus(4, 5, 1).Should().Be(4);
    }

    [Test]
    public void MoveFocus_ShouldClampAtTop_WhenAlreadyOnFirstRow()
    {
        NativeSettingsFocusNavigator.MoveFocus(0, 5, -1).Should().Be(0);
    }

    [Test]
    public void ClampFocus_ShouldReturnNegativeOne_WhenListEmpty()
    {
        NativeSettingsFocusNavigator.ClampFocus(2, 0).Should().Be(-1);
    }

    [Test]
    public void ClampFocus_ShouldClampToLastIndex_WhenOutOfRange()
    {
        NativeSettingsFocusNavigator.ClampFocus(10, 3).Should().Be(2);
    }
}

using K7.Clients.Shared.Services;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AppLifecycleGateTests
{
    [SetUp]
    public void SetUp() => AppLifecycleGate.SetForeground(true);

    [TearDown]
    public void TearDown() => AppLifecycleGate.SetForeground(true);

    [Test]
    public void SetForeground_ShouldRaiseEvent_WhenStateChanges()
    {
        var raised = 0;
        AppLifecycleGate.ForegroundChanged += OnChanged;

        try
        {
            AppLifecycleGate.SetForeground(false);
            AppLifecycleGate.IsForeground.Should().BeFalse();
            raised.Should().Be(1);

            AppLifecycleGate.SetForeground(false);
            raised.Should().Be(1);

            AppLifecycleGate.SetForeground(true);
            AppLifecycleGate.IsForeground.Should().BeTrue();
            raised.Should().Be(2);
        }
        finally
        {
            AppLifecycleGate.ForegroundChanged -= OnChanged;
        }

        void OnChanged() => raised++;
    }
}

using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SettingsAutoSaveTests
{
    private const int DelayMs = 30;
    private const int SettleMs = 300;

    [Test]
    public async Task Schedule_ShouldSaveOnce_WhenFormStaysQuiet()
    {
        using var sut = new SettingsAutoSave(DelayMs);
        var saves = 0;

        sut.Schedule("a", () => { saves++; return Task.CompletedTask; });
        await Task.Delay(SettleMs);

        saves.Should().Be(1);
    }

    [Test]
    public async Task Schedule_ShouldNotSaveAgain_WhenSnapshotIsUnchanged()
    {
        using var sut = new SettingsAutoSave(DelayMs);
        var saves = 0;
        Task Save() { saves++; return Task.CompletedTask; }

        sut.Schedule("a", Save);
        await Task.Delay(SettleMs);
        sut.Schedule("a", Save);
        await Task.Delay(SettleMs);

        saves.Should().Be(1);
    }

    [Test]
    public async Task Schedule_ShouldSaveOnlyTheLastChange_WhenChangesComeInBursts()
    {
        using var sut = new SettingsAutoSave(DelayMs);
        var saved = new List<string>();

        sut.Schedule("a", () => { saved.Add("a"); return Task.CompletedTask; });
        sut.Schedule("b", () => { saved.Add("b"); return Task.CompletedTask; });
        await Task.Delay(SettleMs);

        saved.Should().Equal("b");
    }

    [Test]
    public async Task Schedule_ShouldSaveAgain_WhenSnapshotWasReset()
    {
        using var sut = new SettingsAutoSave(DelayMs);
        var saves = 0;
        Task Save() { saves++; return Task.CompletedTask; }

        sut.Schedule("a", Save);
        await Task.Delay(SettleMs);
        sut.Reset();
        sut.Schedule("a", Save);
        await Task.Delay(SettleMs);

        saves.Should().Be(2);
    }

    [Test]
    public async Task Cancel_ShouldDropThePendingSave()
    {
        using var sut = new SettingsAutoSave(DelayMs);
        var saves = 0;

        sut.Schedule("a", () => { saves++; return Task.CompletedTask; });
        sut.Cancel();
        await Task.Delay(SettleMs);

        saves.Should().Be(0);
    }
}

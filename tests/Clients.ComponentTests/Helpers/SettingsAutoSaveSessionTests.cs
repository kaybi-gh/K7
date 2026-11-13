using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SettingsAutoSaveSessionTests
{
    private const int DelayMs = 30;
    private const int SettleMs = 300;

    private sealed class Form
    {
        public string Value { get; set; } = "";
    }

    private sealed class Page
    {
        public Form Form { get; } = new();
        public SettingsFormTracker<Form> Tracker { get; } = new();
        public List<string> Sent { get; } = [];
        public List<Exception> Errors { get; } = [];
        public Exception? PersistFailure { get; set; }

        public SettingsAutoSaveSession<Form> CreateSession()
        {
            Tracker.Capture(Form);
            return new SettingsAutoSaveSession<Form>(
                Tracker,
                () => Form,
                () =>
                {
                    if (PersistFailure is not null)
                        throw PersistFailure;

                    Sent.Add(Form.Value);
                    return Task.CompletedTask;
                },
                work => work(),
                () => { },
                Errors.Add,
                DelayMs);
        }
    }

    [Test]
    public void State_ShouldBeIdle_WhenNothingChanged()
    {
        var page = new Page();
        using var sut = page.CreateSession();

        sut.OnAfterRender();

        sut.State.Should().Be(SettingsSaveState.Idle);
    }

    [Test]
    public void State_ShouldBeSaving_WhenAChangeWaitsForTheDelay()
    {
        var page = new Page();
        using var sut = page.CreateSession();

        page.Form.Value = "a";
        sut.OnAfterRender();

        sut.State.Should().Be(SettingsSaveState.Saving);
        page.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task OnAfterRender_ShouldSaveTheLastChange_WhenFormStaysQuiet()
    {
        var page = new Page();
        using var sut = page.CreateSession();

        page.Form.Value = "a";
        sut.OnAfterRender();
        page.Form.Value = "ab";
        sut.OnAfterRender();
        await Task.Delay(SettleMs);

        page.Sent.Should().Equal("ab");
        sut.State.Should().Be(SettingsSaveState.Saved);
        sut.IsDirty.Should().BeFalse();
    }

    [Test]
    public async Task SaveAsync_ShouldReportTheErrorAndKeepTheFormDirty_WhenPersistFails()
    {
        var page = new Page { PersistFailure = new InvalidOperationException("offline") };
        using var sut = page.CreateSession();

        page.Form.Value = "a";
        await sut.SaveAsync();

        sut.State.Should().Be(SettingsSaveState.Failed);
        sut.IsDirty.Should().BeTrue();
        page.Errors.Should().ContainSingle();
        page.PersistFailure = null;
    }

    [Test]
    public async Task RunAsync_ShouldDropThePendingSave_WhenTheFormIsReplaced()
    {
        var page = new Page();
        using var sut = page.CreateSession();

        page.Form.Value = "a";
        sut.OnAfterRender();
        await sut.RunAsync(() =>
        {
            page.Form.Value = "";
            page.Tracker.Capture(page.Form);
            return Task.CompletedTask;
        });
        await Task.Delay(SettleMs);

        page.Sent.Should().BeEmpty();
        sut.State.Should().Be(SettingsSaveState.Idle);
    }

    [Test]
    public void Dispose_ShouldSendTheChange_WhenLeavingInsideTheDelay()
    {
        var page = new Page();
        var sut = page.CreateSession();

        page.Form.Value = "a";
        sut.OnAfterRender();
        sut.Dispose();

        page.Sent.Should().Equal("a");
    }

    [Test]
    public void Dispose_ShouldSendNothing_WhenTheFormIsClean()
    {
        var page = new Page();
        var sut = page.CreateSession();

        sut.Dispose();

        page.Sent.Should().BeEmpty();
    }
}

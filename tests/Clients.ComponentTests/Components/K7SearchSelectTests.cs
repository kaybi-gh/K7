using AngleSharp.Dom;
using Bunit;
using K7.Clients.Shared.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7SearchSelectTests
{
    [Test]
    public void Options_ShouldNotBeSpatialNavFocusable()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7SearchSelect>(p => p
            .Add(x => x.SearchAsync, (_, _) => Task.FromResult<IReadOnlyList<string>>(["Alpha", "Beta"])));

        cut.FindAll(".k7-search-select-option").Count.Should().Be(0);

        cut.Find("input").HasAttribute("data-sn-activatable").Should().BeTrue();
        cut.Find("input").HasAttribute("readonly").Should().BeTrue();
    }

    [Test]
    public async Task FocusWithoutEnter_ShouldNotSearch()
    {
        var searchCount = 0;
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7SearchSelect>(p => p
            .Add(x => x.MinSearchLength, 0)
            .Add(x => x.SearchAsync, (_, _) =>
            {
                searchCount++;
                return Task.FromResult<IReadOnlyList<string>>(["Actor A"]);
            }));

        await cut.Find("input").FocusAsync();
        await Task.Delay(100);

        searchCount.Should().Be(0);
        cut.FindAll(".k7-search-select-option").Count.Should().Be(0);
    }

    [Test]
    public async Task Click_ShouldBeginEditingAndSearch()
    {
        var searchCount = 0;
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7SearchSelect>(p => p
            .Add(x => x.MinSearchLength, 0)
            .Add(x => x.SearchAsync, (_, _) =>
            {
                searchCount++;
                return Task.FromResult<IReadOnlyList<string>>(["Actor A"]);
            }));

        await cut.Find("input").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => cut.FindAll(".k7-search-select-option").Count.Should().Be(1));

        searchCount.Should().Be(1);
    }

    [Test]
    public async Task CommitOnSelectOnly_ShouldNotInvokeCommitOnDebouncedSearch()
    {
        var commitCount = 0;
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7SearchSelect>(p => p
            .Add(x => x.DebounceInterval, 50)
            .Add(x => x.MinSearchLength, 2)
            .Add(x => x.CommitOnSelectOnly, true)
            .Add(x => x.OnDebouncedCommit, EventCallback.Factory.Create<string?>(this, _ => commitCount++))
            .Add(x => x.SearchAsync, (_, _) => Task.FromResult<IReadOnlyList<string>>(["Actor A"])));

        var input = cut.Find("input");
        await BeginEditingAsync(input);
        await input.InputAsync("tom");
        cut.WaitForAssertion(() => cut.FindAll(".k7-search-select-option").Count.Should().Be(1));

        commitCount.Should().Be(0);

        await cut.InvokeAsync(() => cut.Find(".k7-search-select-option").Click());
        commitCount.Should().Be(1);
    }

    [Test]
    public async Task MinSearchLengthZero_ShouldSearchOnEnter()
    {
        var searchCount = 0;
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7SearchSelect>(p => p
            .Add(x => x.DebounceInterval, 50)
            .Add(x => x.MinSearchLength, 0)
            .Add(x => x.SearchAsync, (_, _) =>
            {
                searchCount++;
                return Task.FromResult<IReadOnlyList<string>>(["Actor A", "Actor B"]);
            }));

        var input = cut.Find("input");
        await input.FocusAsync();
        searchCount.Should().Be(0);

        await BeginEditingAsync(input);
        cut.WaitForAssertion(() => cut.FindAll(".k7-search-select-option").Count.Should().Be(2));

        searchCount.Should().Be(1);
    }

    [Test]
    public async Task MinSearchLength_ShouldNotSearchBelowThreshold()
    {
        var searchCount = 0;
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7SearchSelect>(p => p
            .Add(x => x.DebounceInterval, 50)
            .Add(x => x.MinSearchLength, 2)
            .Add(x => x.SearchAsync, (_, _) =>
            {
                searchCount++;
                return Task.FromResult<IReadOnlyList<string>>(["Actor A"]);
            }));

        var input = cut.Find("input");
        await BeginEditingAsync(input);
        searchCount.Should().Be(0);

        await input.InputAsync("t");
        await Task.Delay(150);

        searchCount.Should().Be(0);
        cut.FindAll(".k7-search-select-option").Count.Should().Be(0);
    }

    [Test]
    public async Task ArrowDown_ShouldHighlightNextOption()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7SearchSelect>(p => p
            .Add(x => x.DebounceInterval, 50)
            .Add(x => x.MinSearchLength, 2)
            .Add(x => x.SearchAsync, (_, _) => Task.FromResult<IReadOnlyList<string>>(["Alpha", "Beta"])));

        var input = cut.Find("input");
        await BeginEditingAsync(input);
        await input.InputAsync("ab");
        cut.WaitForAssertion(() => cut.FindAll(".k7-search-select-option").Count.Should().Be(2));

        cut.Find(".k7-search-select-option--active").TextContent.Should().Be("Alpha");

        await input.KeyDownAsync("ArrowDown");
        cut.Find(".k7-search-select-option--active").TextContent.Should().Be("Beta");
    }

    [Test]
    public async Task Escape_ShouldCloseDropdownWithoutCommit()
    {
        var commitCount = 0;
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7SearchSelect>(p => p
            .Add(x => x.DebounceInterval, 50)
            .Add(x => x.MinSearchLength, 2)
            .Add(x => x.CommitOnSelectOnly, true)
            .Add(x => x.OnDebouncedCommit, EventCallback.Factory.Create<string?>(this, _ => commitCount++))
            .Add(x => x.SearchAsync, (_, _) => Task.FromResult<IReadOnlyList<string>>(["Actor A"])));

        var input = cut.Find("input");
        await BeginEditingAsync(input);
        await input.InputAsync("tom");
        cut.WaitForAssertion(() => cut.FindAll(".k7-search-select-option").Count.Should().Be(1));

        await input.KeyDownAsync("Escape");
        cut.FindAll(".k7-search-select-option").Count.Should().Be(0);
        commitCount.Should().Be(0);
    }

    private static async Task BeginEditingAsync(IElement input)
    {
        await input.KeyDownAsync("Enter");
    }
}

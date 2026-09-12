using K7.Clients.Shared.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7GroupedListTests
{
    [Test]
    public void Render_ShouldShowGroupHeadersAndItems()
    {
        using var ctx = new BunitContext();
        IReadOnlyList<K7GroupedListGroup<string>> groups =
        [
            new() { Key = "Media", Label = "Media", Items = ["Media.Title", "Media.Year"] },
            new() { Key = "User", Label = "User", Items = ["User.Name"] }
        ];

        var cut = ctx.Render<K7GroupedList<string>>(parameters => parameters
            .Add(p => p.Groups, groups)
            .Add(p => p.ItemLabel, item => item)
            .Add(p => p.Searchable, false));

        cut.Markup.Should().Contain("Media");
        cut.Markup.Should().Contain("Media.Title");
        cut.Markup.Should().Contain("User.Name");
    }

    [Test]
    public void Click_ShouldRaiseItemClicked()
    {
        using var ctx = new BunitContext();
        string? clicked = null;
        IReadOnlyList<K7GroupedListGroup<string>> groups =
        [
            new() { Key = "Media", Label = "Media", Items = ["Media.Title"] }
        ];

        var cut = ctx.Render<K7GroupedList<string>>(parameters => parameters
            .Add(p => p.Groups, groups)
            .Add(p => p.ItemLabel, item => item)
            .Add(p => p.Searchable, false)
            .Add(p => p.ItemClicked, EventCallback.Factory.Create<string>(this, v => clicked = v)));

        cut.Find(".k7-grouped-list__item").Click();
        clicked.Should().Be("Media.Title");
    }
}

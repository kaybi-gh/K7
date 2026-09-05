using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7SelectTests
{
    [Test]
    public void Render_ShouldHaveActivatableAttribute()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());

        // Act
        var cut = ctx.Render<K7Select<string>>();

        // Assert
        var select = cut.Find("button.k7-select");
        select.ClassList.Should().Contain("focusable");
    }

    [Test]
    public void Render_ShouldDisplayItemText_NotValue_OnFirstRender()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());

        // Act
        var cut = ctx.Render(builder =>
        {
            builder.OpenComponent<K7Select<int>>(0);
            builder.AddAttribute(1, nameof(K7Select<int>.Value), 2);
            builder.AddAttribute(2, nameof(K7Select<int>.ChildContent), (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<K7SelectItem<int>>(0);
                childBuilder.AddAttribute(1, nameof(K7SelectItem<int>.Value), 1);
                childBuilder.AddAttribute(2, nameof(K7SelectItem<int>.Text), "One");
                childBuilder.CloseComponent();

                childBuilder.OpenComponent<K7SelectItem<int>>(0);
                childBuilder.AddAttribute(1, nameof(K7SelectItem<int>.Value), 2);
                childBuilder.AddAttribute(2, nameof(K7SelectItem<int>.Text), "Two");
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        // Assert
        cut.Find(".k7-select-text").TextContent.Should().Be("Two");
    }

    [Test]
    public void Render_ShouldShowHelperText_WhenProvided()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());

        // Act
        var cut = ctx.Render(builder =>
        {
            builder.OpenComponent<K7Select<string>>(0);
            builder.AddAttribute(1, nameof(K7Select<string>.HelperText), "Pick a value");
            builder.CloseComponent();
        });

        // Assert
        cut.Find(".k7-field-helper").TextContent.Should().Be("Pick a value");
    }

    [Test]
    public async Task Toggle_ShouldKeepPlacedAndTeleportedClasses_WhenOpened()
    {
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7Select<string>>(p => p.Add(c => c.Class, "library-toolbar-sort"));
        var button = cut.Find("button.k7-select");

        await cut.InvokeAsync(() => button.Click());
        await cut.WaitForAssertionAsync(() =>
        {
            var dropdown = cut.Find(".k7-select-dropdown");
            dropdown.ClassList.Should().Contain("k7-select-dropdown--open");
            dropdown.ClassList.Should().Contain("k7-select-dropdown--placed");
            dropdown.ClassList.Should().Contain("k7-select-dropdown--teleported");
        });

        cut.Render();

        var afterRender = cut.Find(".k7-select-dropdown");
        afterRender.ClassList.Should().Contain("k7-select-dropdown--placed");
        afterRender.ClassList.Should().Contain("k7-select-dropdown--teleported");
    }

    [Test]
    public async Task Toggle_ShouldDropPlacedAndTeleportedClasses_WhenClosed()
    {
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<K7Select<string>>();
        var button = cut.Find("button.k7-select");

        await cut.InvokeAsync(() => button.Click());
        await cut.WaitForAssertionAsync(() =>
            cut.Find(".k7-select-dropdown").ClassList.Should().Contain("k7-select-dropdown--placed"));

        await cut.InvokeAsync(() => button.Click());
        await cut.WaitForAssertionAsync(() =>
        {
            var dropdown = cut.Find(".k7-select-dropdown");
            dropdown.ClassList.Should().NotContain("k7-select-dropdown--open");
            dropdown.ClassList.Should().NotContain("k7-select-dropdown--placed");
            dropdown.ClassList.Should().NotContain("k7-select-dropdown--teleported");
        });
    }
}

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
}

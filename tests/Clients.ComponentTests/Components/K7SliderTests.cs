using K7.Clients.Shared.UI.Components;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7SliderTests
{
    [Test]
    public void Render_ShouldHaveActivatableAttribute()
    {
        // Arrange
        using var ctx = new BunitContext();

        // Act
        var cut = ctx.Render<K7Slider<double>>();

        // Assert
        var input = cut.Find("input[type='range']");
        input.HasAttribute("data-sn-activatable").Should().BeTrue();
    }

    [Test]
    public void Render_WithLabelAndFormatValue_ShouldShowInlineValue()
    {
        using var ctx = new BunitContext();

        var cut = ctx.Render<K7Slider<double>>(parameters => parameters
            .Add(p => p.Label, "Volume")
            .Add(p => p.Value, -14.0)
            .Add(p => p.FormatValue, v => $"{v:F0} LUFS")
            .Add(p => p.Min, 0.0)
            .Add(p => p.Max, 100.0));

        cut.Markup.Should().Contain("Volume");
        cut.Markup.Should().Contain("-14 LUFS");
    }
}

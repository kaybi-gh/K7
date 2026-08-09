using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MediaCardVisualMergeTests
{
    [Test]
    public void Apply_ShouldReturnNext_WhenExistingIsNull()
    {
        var next = CreateCard(pictureUrl: "https://k7.local/p.jpg");

        var result = MediaCardVisualMerge.Apply(null, next);

        result.Model.Should().BeSameAs(next);
        result.RequiresRender.Should().BeTrue();
    }

    [Test]
    public void Apply_ShouldKeepSameInstance_WhenOnlyProgressChanged()
    {
        var existing = CreateCard(pictureUrl: "https://k7.local/p.jpg", progress: 10, watched: false);
        var next = CreateCard(pictureUrl: "https://k7.local/p.jpg?v=999", progress: 42, watched: true);

        var result = MediaCardVisualMerge.Apply(existing, next);

        result.Model.Should().BeSameAs(existing);
        result.RequiresRender.Should().BeTrue();
        existing.Progress.Should().Be(42);
        existing.Watched.Should().BeTrue();
        existing.PictureUrl.Should().Be("https://k7.local/p.jpg");
    }

    [Test]
    public void Apply_ShouldNotRequireRender_WhenNothingChanged()
    {
        var existing = CreateCard(pictureUrl: "https://k7.local/p.jpg", progress: 10);
        var next = CreateCard(pictureUrl: "https://k7.local/p.jpg?v=1", progress: 10);

        var result = MediaCardVisualMerge.Apply(existing, next);

        result.Model.Should().BeSameAs(existing);
        result.RequiresRender.Should().BeFalse();
    }

    [Test]
    public void Apply_ShouldPreservePictureUrl_WhenTitleChangesButSameResource()
    {
        var existing = CreateCard(title: "Old", pictureUrl: "https://k7.local/p.jpg");
        var next = CreateCard(title: "New", pictureUrl: "https://k7.local/p.jpg?v=123");

        var result = MediaCardVisualMerge.Apply(existing, next);

        result.RequiresRender.Should().BeTrue();
        result.Model.Should().NotBeSameAs(existing);
        result.Model.Title.Should().Be("New");
        result.Model.PictureUrl.Should().Be("https://k7.local/p.jpg");
    }

    [Test]
    public void Apply_ShouldUseNewPictureUrl_WhenResourceChanged()
    {
        var existing = CreateCard(pictureUrl: "https://k7.local/old.jpg");
        var next = CreateCard(pictureUrl: "https://k7.local/new.jpg");

        var result = MediaCardVisualMerge.Apply(existing, next);

        result.RequiresRender.Should().BeTrue();
        result.Model.PictureUrl.Should().Be("https://k7.local/new.jpg");
    }

    private static MediaCardViewModel CreateCard(
        string title = "Title",
        string? pictureUrl = null,
        string? backdropUrl = null,
        double progress = 0,
        bool watched = false) =>
        new()
        {
            Id = "11111111-1111-1111-1111-111111111111",
            Title = title,
            PictureUrl = pictureUrl,
            BackdropUrl = backdropUrl,
            Progress = progress,
            Watched = watched
        };
}

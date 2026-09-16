using AwesomeAssertions;
using K7.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class LibraryPathMirrorTests
{
    [Test]
    public void TryGetRelativePath_ShouldReturnForwardSlashPath()
    {
        LibraryPathMirror.TryGetRelativePath(
                @"D:\media\movies\Ambulance (2022)\Ambulance.mkv",
                @"D:\media\movies")
            .Should().Be("Ambulance (2022)/Ambulance.mkv");

        LibraryPathMirror.TryGetRelativePath(
                "/data/movies/Ambulance (2022)/Ambulance.mkv",
                "/data/movies")
            .Should().Be("Ambulance (2022)/Ambulance.mkv");
    }

    [Test]
    public void TryGetRelativePath_ShouldRejectTraversalAndOutsideRoot()
    {
        LibraryPathMirror.TryGetRelativePath(@"D:\other\file.mkv", @"D:\media\movies").Should().BeNull();
        LibraryPathMirror.TryGetRelativePath("/data/movies/../secret/file.mkv", "/data/movies").Should().BeNull();
        LibraryPathMirror.TryGetRelativePath(null, "/data").Should().BeNull();
    }

    [Test]
    public void TryCombine_ShouldJoinLocalRootAndRelative()
    {
        var combined = LibraryPathMirror.TryCombine(@"K:\movies", "Ambulance (2022)/Ambulance.mkv");

        combined.Should().Be(Path.Combine(@"K:\movies", "Ambulance (2022)", "Ambulance.mkv"));
    }

    [Test]
    public void TryCombine_ShouldRejectTraversal()
    {
        LibraryPathMirror.TryCombine(@"K:\movies", "../secret/file.mkv").Should().BeNull();
    }

    [Test]
    public void Serialize_ShouldRoundTripNonEmptyRoots()
    {
        var movies = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var json = LibraryPathMirror.Serialize(new Dictionary<Guid, string>
        {
            [movies] = @"K:\movies",
            [Guid.NewGuid()] = "  "
        });

        var parsed = LibraryPathMirror.Deserialize(json);
        parsed.Should().ContainKey(movies).WhoseValue.Should().Be(@"K:\movies");
        parsed.Should().HaveCount(1);
        LibraryPathMirror.Deserialize("").Should().BeEmpty();
        LibraryPathMirror.Deserialize("{not-json").Should().BeEmpty();
    }
}

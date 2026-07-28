using FluentAssertions;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;

namespace K7.Server.Application.UnitTests.Helpers;

/// <summary>
/// The key must match the criteria the creation handler uses to find an existing media, otherwise two
/// commands resolving to the same media would take different locks and could both create it.
/// </summary>
public class MediaIdentityKeyTests
{
    private static readonly Guid LibraryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherLibraryId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public void Build_ShouldMatchForTracksOfTheSameAlbum_EvenInDifferentBatches()
    {
        var first = File("/music/Abbey Road/01.flac", new MediaIdentification("Come Together")
        {
            AlbumName = "Abbey Road",
            ArtistName = "The Beatles",
            TrackNumber = 1
        });
        var second = File("/music/Abbey Road/09.flac", new MediaIdentification("Because")
        {
            AlbumName = "abbey road",
            ArtistName = "the beatles",
            TrackNumber = 9
        });

        MediaIdentityKey.Build(MediaType.MusicAlbum, LibraryId, [first])
            .Should().Be(MediaIdentityKey.Build(MediaType.MusicAlbum, LibraryId, [second]));
    }

    [Test]
    public void Build_ShouldMatchForEpisodesOfTheSameSerie_RegardlessOfSeasonAndEpisode()
    {
        var s01e01 = File("/series/The Wire/S01E01.mkv", new MediaIdentification("Episode")
        {
            SeriesTitle = "The Wire",
            SeasonNumber = 1,
            EpisodeNumber = 1
        });
        var s04e12 = File("/series/The Wire/S04E12.mkv", new MediaIdentification("Episode")
        {
            SeriesTitle = "The Wire",
            SeasonNumber = 4,
            EpisodeNumber = 12
        });

        MediaIdentityKey.Build(MediaType.Serie, LibraryId, [s01e01])
            .Should().Be(MediaIdentityKey.Build(MediaType.Serie, LibraryId, [s04e12]));
    }

    [Test]
    public void Build_ShouldDifferForDifferentAlbumsOfTheSameArtist()
    {
        var abbeyRoad = File("/music/a/1.flac", new MediaIdentification("Come Together")
        {
            AlbumName = "Abbey Road",
            ArtistName = "The Beatles"
        });
        var revolver = File("/music/b/1.flac", new MediaIdentification("Taxman")
        {
            AlbumName = "Revolver",
            ArtistName = "The Beatles"
        });

        MediaIdentityKey.Build(MediaType.MusicAlbum, LibraryId, [abbeyRoad])
            .Should().NotBe(MediaIdentityKey.Build(MediaType.MusicAlbum, LibraryId, [revolver]));
    }

    [Test]
    public void Build_ShouldDifferAcrossLibraries()
    {
        var file = File("/movies/Inception.mkv", new MediaIdentification("Inception")
        {
            ReleaseYear = new DateOnly(2010, 1, 1)
        });

        MediaIdentityKey.Build(MediaType.Movie, LibraryId, [file])
            .Should().NotBe(MediaIdentityKey.Build(MediaType.Movie, OtherLibraryId, [file]));
    }

    [Test]
    public void Build_ShouldDifferForSameTitleDifferentYear()
    {
        var original = File("/movies/Dune (1984).mkv", new MediaIdentification("Dune")
        {
            ReleaseYear = new DateOnly(1984, 1, 1)
        });
        var remake = File("/movies/Dune (2021).mkv", new MediaIdentification("Dune")
        {
            ReleaseYear = new DateOnly(2021, 1, 1)
        });

        MediaIdentityKey.Build(MediaType.Movie, LibraryId, [original])
            .Should().NotBe(MediaIdentityKey.Build(MediaType.Movie, LibraryId, [remake]));
    }

    [Test]
    public void Build_ShouldFallBackToParentDirectory_WhenAlbumNameIsMissing()
    {
        var first = File("/music/Unknown Album/01.flac", new MediaIdentification("Track 1")
        {
            ArtistName = "Artist"
        }, parentDirectory: "/music/Unknown Album");
        var second = File("/music/Unknown Album/02.flac", new MediaIdentification("Track 2")
        {
            ArtistName = "Artist"
        }, parentDirectory: "/music/Unknown Album");

        MediaIdentityKey.Build(MediaType.MusicAlbum, LibraryId, [first])
            .Should().Be(MediaIdentityKey.Build(MediaType.MusicAlbum, LibraryId, [second]));
    }

    [Test]
    public void Build_ShouldFallBackToPath_WhenNothingIsIdentified()
    {
        var first = File("/movies/a.mkv", identification: null);
        var second = File("/movies/b.mkv", identification: null);

        var firstKey = MediaIdentityKey.Build(MediaType.Movie, LibraryId, [first]);

        // Unidentified files must not all collide on one key, which would serialize the whole scan.
        firstKey.Should().NotBe(MediaIdentityKey.Build(MediaType.Movie, LibraryId, [second]));
        firstKey.Should().Contain("path:");
    }

    [Test]
    public void Build_ShouldNotThrow_WhenNoFilesAreProvided()
    {
        var act = () => MediaIdentityKey.Build(MediaType.Movie, LibraryId, []);

        act.Should().NotThrow();
    }

    private static IndexedFile File(string path, MediaIdentification? identification, string? parentDirectory = null)
        => new()
        {
            Id = Guid.NewGuid(),
            LibraryId = LibraryId,
            Name = Path.GetFileNameWithoutExtension(path),
            Extension = Path.GetExtension(path),
            Path = path,
            ParentDirectory = parentDirectory ?? Path.GetDirectoryName(path)!,
            Hash = 1u,
            Size = 1,
            Identification = identification
        };
}

using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;

namespace K7.Server.Application.UnitTests.Helpers;

public class CatalogMediaAvailabilityHelperTests
{
    [Test]
    public void HasPlayableFiles_ShouldReturnFalse_WhenMovieHasNoIndexedFiles()
    {
        var movie = new Movie { IndexedFiles = [] };

        CatalogMediaAvailabilityHelper.HasPlayableFiles(movie).Should().BeFalse();
    }

    [Test]
    public void HasPlayableFiles_ShouldReturnTrue_WhenMovieHasIndexedFile()
    {
        var movie = new Movie
        {
            IndexedFiles = [new IndexedFile { LibraryId = Guid.NewGuid() }]
        };

        CatalogMediaAvailabilityHelper.HasPlayableFiles(movie).Should().BeTrue();
    }

    [Test]
    public void HasPlayableFiles_ShouldReturnFalse_WhenAllIndexedFilesAreExcluded()
    {
        var libraryId = Guid.NewGuid();
        var movie = new Movie
        {
            IndexedFiles = [new IndexedFile { LibraryId = libraryId }]
        };

        CatalogMediaAvailabilityHelper.HasPlayableFiles(movie, [libraryId]).Should().BeFalse();
    }

    [Test]
    public void HasPlayableFiles_ShouldReturnTrue_WhenSerieHasEpisodeWithIndexedFile()
    {
        var serie = new Serie
        {
            Seasons =
            [
                new SerieSeason
                {
                    Episodes =
                    [
                        new SerieEpisode
                        {
                            IndexedFiles = [new IndexedFile { LibraryId = Guid.NewGuid() }]
                        }
                    ]
                }
            ]
        };

        CatalogMediaAvailabilityHelper.HasPlayableFiles(serie).Should().BeTrue();
    }

    [Test]
    public void HasPlayableFiles_ShouldReturnTrue_WhenMusicArtistHasAlbumWithPlayableTrack()
    {
        var artist = new MusicArtist
        {
            Albums =
            [
                new MusicAlbum
                {
                    Tracks =
                    [
                        new MusicTrack
                        {
                            IndexedFiles = [new IndexedFile { LibraryId = Guid.NewGuid() }]
                        }
                    ]
                }
            ]
        };

        CatalogMediaAvailabilityHelper.HasPlayableFiles(artist).Should().BeTrue();
    }

    [Test]
    public void HasPlayableFiles_ShouldReturnFalse_WhenMusicArtistHasOnlyEmptyAlbums()
    {
        var artist = new MusicArtist
        {
            Albums = [new MusicAlbum { Tracks = [new MusicTrack()] }]
        };

        CatalogMediaAvailabilityHelper.HasPlayableFiles(artist).Should().BeFalse();
    }
}

public class PersonRoleAvailabilityHelperTests
{
    [Test]
    public void FilterPlayableRoles_ShouldExcludeRolesWithoutIndexedFiles()
    {
        var personId = Guid.NewGuid();
        var roles = new List<BasePersonRole>
        {
            new Actor
            {
                PersonId = personId,
                Media = new Movie
                {
                    Title = "Available",
                    IndexedFiles = [new IndexedFile { LibraryId = Guid.NewGuid() }]
                }
            },
            new Actor
            {
                PersonId = personId,
                Media = new Movie
                {
                    Title = "Unavailable",
                    IndexedFiles = []
                }
            }
        };

        var filtered = PersonRoleAvailabilityHelper.FilterPlayableRoles(roles);

        filtered.Should().ContainSingle();
        filtered[0].Media!.Title.Should().Be("Available");
    }

    [Test]
    public void FilterPlayableRoles_ShouldDedupeRolesByTmdbExternalId()
    {
        var personId = Guid.NewGuid();
        var tmdbId = "12345";
        var roles = new List<BasePersonRole>
        {
            new Actor
            {
                PersonId = personId,
                Media = new Movie
                {
                    Title = "Duplicate A",
                    IndexedFiles = [new IndexedFile { LibraryId = Guid.NewGuid() }],
                    ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = tmdbId }]
                }
            },
            new Actor
            {
                PersonId = personId,
                Media = new Movie
                {
                    Title = "Duplicate B",
                    IndexedFiles = [new IndexedFile { LibraryId = Guid.NewGuid() }],
                    ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = tmdbId }]
                }
            }
        };

        var filtered = PersonRoleAvailabilityHelper.FilterPlayableRoles(roles);

        filtered.Should().ContainSingle();
    }

    [Test]
    public void FilterPlayableRoles_ShouldKeepMusicArtistRole_WhenAlbumTracksArePlayable()
    {
        var personId = Guid.NewGuid();
        var roles = new List<BasePersonRole>
        {
            new MusicArtistMember
            {
                PersonId = personId,
                Media = new MusicArtist
                {
                    Title = "Artist",
                    Albums =
                    [
                        new MusicAlbum
                        {
                            Title = "Playable Album",
                            Tracks =
                            [
                                new MusicTrack
                                {
                                    IndexedFiles = [new IndexedFile { LibraryId = Guid.NewGuid() }]
                                }
                            ]
                        },
                        new MusicAlbum
                        {
                            Title = "Empty Album",
                            Tracks = [new MusicTrack()]
                        }
                    ]
                }
            }
        };

        var filtered = PersonRoleAvailabilityHelper.FilterPlayableRoles(roles);

        filtered.Should().ContainSingle();
        var artist = (MusicArtist)filtered[0].Media!;
        artist.Albums.Should().ContainSingle();
        artist.Albums[0].Title.Should().Be("Playable Album");
    }
}

using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;

namespace K7.Server.Application.UnitTests.Common.Services;

[TestFixture]
public class MediaRatingScopeTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();

    [Test]
    public void ScopeToUser_ShouldKeepOnlyTheCallersRatingAndProviderRatings_WhenSeveralUsersRated()
    {
        var movie = new Movie { Id = Guid.NewGuid(), Title = "Film" };
        movie.Ratings.Add(Rating(_other, 2));
        movie.Ratings.Add(Rating(_me, 8));
        movie.Ratings.Add(new MetadataProviderRating { MetadataProvider = default, Value = 7 });

        MediaRatingScope.ScopeToUser(movie, _me);

        movie.Ratings.OfType<UserRating>().Should().ContainSingle().Which.UserId.Should().Be(_me);
        movie.Ratings.OfType<MetadataProviderRating>().Should().ContainSingle();
    }

    [Test]
    public void ScopeToUser_ShouldRemoveEveryUserRating_WhenThereIsNoCurrentUser()
    {
        var movie = new Movie { Id = Guid.NewGuid(), Title = "Film" };
        movie.Ratings.Add(Rating(_other, 2));

        MediaRatingScope.ScopeToUser(movie, null);

        movie.Ratings.Should().BeEmpty();
    }

    [Test]
    public void ScopeToUser_ShouldReachNestedMedias_WhenTheGraphIsLoaded()
    {
        var artist = new MusicArtist { Id = Guid.NewGuid(), Title = "Artist" };
        var album = new MusicAlbum { Id = Guid.NewGuid(), Title = "Album", Artist = artist };
        var track = new MusicTrack { Id = Guid.NewGuid(), Title = "Track", Album = album, AlbumId = album.Id };
        artist.Albums.Add(album);
        album.Tracks.Add(track);
        artist.Ratings.Add(Rating(_other, 2));
        album.Ratings.Add(Rating(_other, 4));
        track.Ratings.Add(Rating(_other, 6));
        track.Ratings.Add(Rating(_me, 10));

        MediaRatingScope.ScopeToUser(artist, _me);

        artist.Ratings.Should().BeEmpty();
        album.Ratings.Should().BeEmpty();
        track.Ratings.Should().ContainSingle().Which.Value.Should().Be(10);
    }

    private static UserRating Rating(Guid userId, double value) =>
        new() { UserId = userId, Value = value };
}

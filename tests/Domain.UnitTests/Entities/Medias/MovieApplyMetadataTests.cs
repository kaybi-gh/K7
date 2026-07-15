using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Helpers;

namespace K7.Server.Domain.UnitTests.Entities.Medias;

public class MovieApplyMetadataTests
{
    [Test]
    public void ApplyMetadata_ShouldUpdateUnlockedFields()
    {
        var movie = new Movie
        {
            Title = "Old",
            Overview = "Old overview",
            Tagline = "Old tag",
            Budget = 1,
            Revenue = 2
        };

        movie.ApplyMetadata(new ExternalMovieMetadata
        {
            Title = "New",
            Overview = "New overview",
            Tagline = "New tag",
            Budget = 100,
            Revenue = 200,
            ReleaseDate = new DateOnly(2020, 1, 1)
        });

        movie.Title.Should().Be("New");
        movie.Overview.Should().Be("New overview");
        movie.Tagline.Should().Be("New tag");
        movie.Budget.Should().Be(100);
        movie.Revenue.Should().Be(200);
        movie.ReleaseDate.Should().Be(new DateOnly(2020, 1, 1));
    }

    [Test]
    public void ApplyMetadata_ShouldSkipLockedFields()
    {
        var movie = new Movie
        {
            Title = "Locked Title",
            Overview = "Locked Overview"
        };
        movie.LockField(nameof(Movie.Title));
        movie.LockField(nameof(Movie.Overview));

        movie.ApplyMetadata(new ExternalMovieMetadata
        {
            Title = "Ignored",
            Overview = "Also ignored",
            Tagline = "Applied"
        });

        movie.Title.Should().Be("Locked Title");
        movie.Overview.Should().Be("Locked Overview");
        movie.Tagline.Should().Be("Applied");
    }

    [Test]
    public void ApplyMetadata_ShouldPreserveFederationExternalIds()
    {
        var movie = new Movie { Title = "Film" };
        movie.ExternalIds.Add(new ExternalId { ProviderName = "federation", Value = "peer:1" });
        movie.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "old" });

        movie.ApplyMetadata(new ExternalMovieMetadata
        {
            Title = "Film",
            ExternalIds =
            [
                new ExternalId { ProviderName = "tmdb", Value = "new" }
            ]
        });

        movie.ExternalIds.Should().ContainSingle(e => e.ProviderName == "tmdb" && e.Value == "new");
        movie.ExternalIds.Should().ContainSingle(e => e.ProviderName == "federation" && e.Value == "peer:1");
        movie.ExternalIds.Should().HaveCount(2);
    }

    [Test]
    public void ApplyMetadata_ShouldNotReplaceExternalIds_WhenFieldLocked()
    {
        var movie = new Movie { Title = "Film" };
        movie.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "keep" });
        movie.LockField(nameof(Movie.ExternalIds));

        movie.ApplyMetadata(new ExternalMovieMetadata
        {
            Title = "Film",
            ExternalIds =
            [
                new ExternalId { ProviderName = "tmdb", Value = "replace" }
            ]
        });

        movie.ExternalIds.Should().ContainSingle(e => e.Value == "keep");
    }

    [Test]
    public void ApplyMetadata_ShouldReplaceUnlockedPictureTypes_AndSkipLockedOnes()
    {
        var movie = new Movie { Title = "Film" };
        movie.Pictures.Add(new MetadataPicture { Type = MetadataPictureType.Poster, LocalPath = "old-poster" });
        movie.Pictures.Add(new MetadataPicture { Type = MetadataPictureType.Backdrop, LocalPath = "old-backdrop" });
        movie.LockedFields.Add(MetadataPictureLockHelper.GetTypeField(MetadataPictureType.Poster));

        movie.ApplyMetadata(new ExternalMovieMetadata
        {
            Title = "Film",
            Pictures =
            [
                new MetadataPicture { Type = MetadataPictureType.Poster, LocalPath = "new-poster" },
                new MetadataPicture { Type = MetadataPictureType.Backdrop, LocalPath = "new-backdrop" }
            ]
        });

        movie.Pictures.Should().ContainSingle(p => p.Type == MetadataPictureType.Poster && p.LocalPath == "old-poster");
        movie.Pictures.Should().ContainSingle(p => p.Type == MetadataPictureType.Backdrop && p.LocalPath == "new-backdrop");
    }
}

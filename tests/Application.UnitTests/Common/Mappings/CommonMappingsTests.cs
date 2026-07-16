using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Common.Mappings;

public class CommonMappingsTests
{
    [Test]
    public void ToPlaylistDto_ShouldMapCoreFields()
    {
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Title = "Favs",
            Description = "Desc",
            MediaType = MediaType.Movie,
            UserId = Guid.NewGuid(),
            VisibilityScope = VisibilityScope.LocalServer
        };
        playlist.Items.Add(new PlaylistItem { MediaId = Guid.NewGuid(), Order = 0 });

        var dto = playlist.ToPlaylistDto();

        dto.Id.Should().Be(playlist.Id);
        dto.Title.Should().Be("Favs");
        dto.Description.Should().Be("Desc");
        dto.MediaType.Should().Be(MediaType.Movie);
        dto.ItemCount.Should().Be(1);
        dto.IsSmartPlaylist.Should().BeFalse();
        dto.VisibilityScope.Should().Be(VisibilityScope.LocalServer);
    }

    [Test]
    public void ToLibraryDto_ShouldMapScanAndProviderSettings()
    {
        var library = new Library
        {
            Id = Guid.NewGuid(),
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            LibraryGroupId = Guid.NewGuid(),
            RootPath = @"C:\media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RealtimeMonitorEnabled = false,
            AutoScanIntervalHours = 0
        };

        var dto = library.ToLibraryDto();

        dto.Title.Should().Be("Movies");
        dto.RootPath.Should().Be(@"C:\media");
        dto.MetadataProviderName.Should().Be("tmdb");
        dto.RealtimeMonitorEnabled.Should().BeFalse();
        dto.AutoScanIntervalHours.Should().Be(0);
    }

    [Test]
    public void ToUserDto_ShouldHidePinHashByDefault()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Ada",
            PinHash = "secret-hash",
            Role = "User"
        };

        var dto = user.ToUserDto();

        dto.DisplayName.Should().Be("Ada");
        dto.HasPin.Should().BeTrue();
        dto.PinHash.Should().BeNull();
        dto.IsGuest.Should().BeFalse();
    }

    [Test]
    public void ToContentRestrictionProfileDto_ShouldIncludeUserCount()
    {
        var profile = new ContentRestrictionProfile
        {
            Id = Guid.NewGuid(),
            Name = "Kids",
            Description = "Safe"
        };
        profile.Users.Add(new User { DisplayName = "a" });
        profile.Users.Add(new User { DisplayName = "b" });

        var dto = profile.ToContentRestrictionProfileDto();

        dto.Name.Should().Be("Kids");
        dto.Description.Should().Be("Safe");
        dto.UserCount.Should().Be(2);
    }
}

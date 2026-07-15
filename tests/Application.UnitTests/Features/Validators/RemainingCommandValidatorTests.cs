using K7.Server.Application.Features.Libraries.Commands.UpdateLibrary;
using K7.Server.Application.Features.Medias.Commands.BulkCreateMedias;
using K7.Server.Application.Features.Medias.Commands.UpdateMediaMetadata;
using K7.Server.Application.Features.Notifications.Commands.CreateNotificationRule;
using K7.Server.Application.Features.Notifications.Commands.UpdateNotificationRule;
using K7.Server.Application.Features.Persons.Commands.UpdatePersonMetadata;
using K7.Server.Application.Features.Playlists.Commands.UpdatePlaylist;
using K7.Server.Application.Features.Users.Commands.MergeUsers;
using K7.Server.Application.Features.Users.Commands.UpdateEmail;
using K7.Server.Application.Features.Users.Commands.UpdateProfile;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Requests;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Validators;

[TestFixture]
public class RemainingCommandValidatorTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public void UpdateEmail_ShouldRequireValidEmailAndPassword()
    {
        var validator = new UpdateEmailCommandValidator();

        validator.Validate(new UpdateEmailCommand
        {
            Email = "not-an-email",
            CurrentPassword = "x"
        }).IsValid.Should().BeFalse();

        validator.Validate(new UpdateEmailCommand
        {
            Email = "kay@example.com",
            CurrentPassword = "secret"
        }).IsValid.Should().BeTrue();
    }

    [Test]
    public void UpdateProfile_ShouldEnforceDisplayNameMaxLength()
    {
        var validator = new UpdateProfileCommandValidator();

        validator.Validate(new UpdateProfileCommand { DisplayName = new string('a', 51) }).IsValid.Should().BeFalse();
        validator.Validate(new UpdateProfileCommand { DisplayName = "Kay" }).IsValid.Should().BeTrue();
    }

    [Test]
    public void MergeUsers_ShouldRequireDistinctNonEmptyIds()
    {
        var validator = new MergeUsersCommandValidator();
        var id = Guid.NewGuid();

        validator.Validate(new MergeUsersCommand(Guid.Empty, Guid.NewGuid())).IsValid.Should().BeFalse();
        validator.Validate(new MergeUsersCommand(id, id)).IsValid.Should().BeFalse();
        validator.Validate(new MergeUsersCommand(id, Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Test]
    public void UpdatePlaylist_ShouldRequireTitle()
    {
        var validator = new UpdatePlaylistCommandValidator();

        validator.Validate(new UpdatePlaylistCommand
        {
            Id = Guid.NewGuid(),
            Title = "",
            MediaType = MediaType.Movie
        }).IsValid.Should().BeFalse();

        validator.Validate(new UpdatePlaylistCommand
        {
            Id = Guid.NewGuid(),
            Title = "Favorites",
            MediaType = MediaType.Movie
        }).IsValid.Should().BeTrue();
    }

    [Test]
    public async Task UpdateLibrary_ShouldEnforceUniqueTitleWhenProvided()
    {
        var groupId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = existingId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Movie,
            Title = "Taken",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        await _context.SaveChangesAsync();

        var validator = new UpdateLibraryCommandValidator(_context);

        var duplicate = await validator.ValidateAsync(new UpdateLibraryCommand
        {
            Id = Guid.NewGuid(),
            Title = "Taken"
        });
        var unique = await validator.ValidateAsync(new UpdateLibraryCommand
        {
            Id = Guid.NewGuid(),
            Title = "New Library"
        });
        var unchanged = await validator.ValidateAsync(new UpdateLibraryCommand
        {
            Id = existingId,
            Title = "Taken"
        });

        duplicate.IsValid.Should().BeFalse();
        unique.IsValid.Should().BeTrue();
        unchanged.IsValid.Should().BeTrue();
    }

    [Test]
    public void BulkCreateMedias_ShouldValidateItems()
    {
        var validator = new BulkCreateMediasCommandValidator();

        validator.Validate(new BulkCreateMediasCommand { Items = [] }).IsValid.Should().BeFalse();

        validator.Validate(new BulkCreateMediasCommand
        {
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "1",
                    MediaType = "film",
                    Title = "X"
                }
            ]
        }).IsValid.Should().BeFalse();

        validator.Validate(new BulkCreateMediasCommand
        {
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "1",
                    MediaType = "movie",
                    Title = "Inception"
                }
            ]
        }).IsValid.Should().BeTrue();
    }

    [Test]
    public void UpdateMediaMetadata_ShouldRequireIdAndLockedFields()
    {
        var validator = new UpdateMediaMetadataCommandValidator();

        validator.Validate(new UpdateMediaMetadataCommand
        {
            Id = Guid.Empty,
            LockedFields = null!
        }).IsValid.Should().BeFalse();

        validator.Validate(new UpdateMediaMetadataCommand
        {
            Id = Guid.NewGuid(),
            LockedFields = [],
            Title = "Ok"
        }).IsValid.Should().BeTrue();
    }

    [Test]
    public void NotificationRules_ShouldValidateProviderAndEventTypes()
    {
        var create = new CreateNotificationRuleCommandValidator();
        var update = new UpdateNotificationRuleCommandValidator();

        create.Validate(new CreateNotificationRuleCommand
        {
            Name = "Rule",
            ProviderType = "Nope",
            PayloadFormat = "Json",
            EventTypeNames = ["MediaCreated"],
            ProviderConfig = "{}"
        }).IsValid.Should().BeFalse();

        create.Validate(new CreateNotificationRuleCommand
        {
            Name = "Rule",
            ProviderType = nameof(NotificationProviderType.Webhook),
            PayloadFormat = "Json",
            EventTypeNames = ["MediaCreated"],
            ProviderConfig = "{}"
        }).IsValid.Should().BeTrue();

        update.Validate(new UpdateNotificationRuleCommand
        {
            Id = Guid.NewGuid(),
            Name = "Rule",
            ProviderType = nameof(NotificationProviderType.Webhook),
            PayloadFormat = "Json",
            EventTypeNames = ["MediaCreated"],
            ProviderConfig = "{}"
        }).IsValid.Should().BeTrue();
    }

    [Test]
    public void UpdatePersonMetadata_ShouldRequireIdAndLockedFields()
    {
        var validator = new UpdatePersonMetadataCommandValidator();

        validator.Validate(new UpdatePersonMetadataCommand
        {
            Id = Guid.Empty,
            LockedFields = null!
        }).IsValid.Should().BeFalse();

        validator.Validate(new UpdatePersonMetadataCommand
        {
            Id = Guid.NewGuid(),
            LockedFields = [],
            Name = "Leonardo DiCaprio"
        }).IsValid.Should().BeTrue();
    }
}

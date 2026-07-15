using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Features.Medias.Commands.RateMedia;
using K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Validators;

[TestFixture]
public class MediaAndLibraryCommandValidatorTests
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
    public async Task CreateLibrary_ShouldRequireTitleRootPathAndUniqueFields()
    {
        var groupId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = Guid.NewGuid(),
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Movie,
            Title = "Existing",
            RootPath = @"C:\media\movies",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        await _context.SaveChangesAsync();

        var validator = new CreateLibraryCommandValidator(_context);

        var missingFields = await validator.ValidateAsync(new CreateLibraryCommand
        {
            Title = "",
            MediaType = LibraryMediaType.Movie,
            RootPath = "",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        var duplicateTitle = await validator.ValidateAsync(ValidLibraryCommand() with { Title = "Existing" });
        var duplicatePath = await validator.ValidateAsync(ValidLibraryCommand() with { RootPath = @"C:\media\movies" });
        var valid = await validator.ValidateAsync(ValidLibraryCommand());

        missingFields.IsValid.Should().BeFalse();
        duplicateTitle.IsValid.Should().BeFalse();
        duplicatePath.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Test]
    public void CreateMedia_ShouldRequireLibraryAndIndexedFiles()
    {
        var validator = new CreateMediaCommandValidator();

        var invalid = validator.Validate(new CreateMediaCommand
        {
            MediaType = MediaType.Movie,
            LibraryId = Guid.Empty,
            IndexedFileIds = []
        });
        var valid = validator.Validate(new CreateMediaCommand
        {
            MediaType = MediaType.Movie,
            LibraryId = Guid.NewGuid(),
            IndexedFileIds = [Guid.NewGuid()]
        });

        invalid.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    [Test]
    public void RateMedia_ShouldRestrictValueBetween0And10()
    {
        var validator = new RateMediaCommandValidator();
        var mediaId = Guid.NewGuid();

        validator.Validate(new RateMediaCommand(mediaId, -1)).IsValid.Should().BeFalse();
        validator.Validate(new RateMediaCommand(mediaId, 11)).IsValid.Should().BeFalse();
        validator.Validate(new RateMediaCommand(mediaId, 7)).IsValid.Should().BeTrue();
    }

    [Test]
    public void CreatePlaylist_ShouldRequireTitle()
    {
        var validator = new CreatePlaylistCommandValidator();

        validator.Validate(new CreatePlaylistCommand
        {
            Title = "",
            MediaType = MediaType.Movie
        }).IsValid.Should().BeFalse();

        validator.Validate(new CreatePlaylistCommand
        {
            Title = "Favorites",
            MediaType = MediaType.Movie
        }).IsValid.Should().BeTrue();
    }

    private static CreateLibraryCommand ValidLibraryCommand() => new()
    {
        Title = "New Library",
        MediaType = LibraryMediaType.Movie,
        RootPath = @"D:\media\new",
        MetadataProviderName = "tmdb",
        MetadataLanguage = "fr",
        MetadataFallbackLanguage = "en"
    };
}

using K7.Server.Application.Helpers;
using K7.Server.Application.Models;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.UnitTests.Helpers;

public class LibraryScanDiffBuilderRemovalTests
{
    [Test]
    public void Build_ShouldMarkMissingExistingFileAsRemoved_WhenPathUsesMixedSeparators()
    {
        var existing = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = Guid.NewGuid(),
            Name = "track",
            Extension = ".flac",
            Path = @"C:/music/artist/album/track.flac",
            ParentDirectory = "album",
            Hash = 42,
            Size = 1000,
            LastWriteTimeUtc = DateTimeOffset.UtcNow,
            Created = DateTimeOffset.UtcNow
        };

        var diff = LibraryScanDiffBuilder.Build([], [existing], [], _ => throw new InvalidOperationException());

        diff.RemovedFiles.Should().ContainSingle().Which.Should().BeSameAs(existing);
    }

    [Test]
    public void Build_ShouldTreatScannedAndExistingPathsAsSame_WhenSeparatorsDiffer()
    {
        var existing = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = Guid.NewGuid(),
            Name = "track",
            Extension = ".flac",
            Path = @"C:/music/artist/album/track.flac",
            ParentDirectory = "album",
            Hash = 42,
            Size = 1000,
            LastWriteTimeUtc = DateTimeOffset.UtcNow,
            Created = DateTimeOffset.UtcNow
        };

        var scanned = new ScannedFileEntry
        {
            Path = @"C:\music\artist\album\track.flac",
            Name = "track",
            Extension = ".flac",
            ParentDirectory = "album",
            Size = 1000,
            LastWriteTimeUtc = existing.LastWriteTimeUtc
        };

        var diff = LibraryScanDiffBuilder.Build(
            [scanned],
            [existing],
            [],
            _ => throw new InvalidOperationException(),
            libraryRootPath: @"C:\music");

        diff.UnchangedFiles.Should().ContainSingle().Which.Should().BeSameAs(existing);
        diff.RemovedFiles.Should().BeEmpty();
        diff.AddedFiles.Should().BeEmpty();
    }
}

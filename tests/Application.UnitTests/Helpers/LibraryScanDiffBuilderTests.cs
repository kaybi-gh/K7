using K7.Server.Application.Helpers;
using K7.Server.Application.Models;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.UnitTests.Helpers;

public class LibraryScanDiffBuilderTests
{
    private const string FilePath = @"C:\k7\movies\A Minecraft Movie (2025)\A Minecraft Movie (2025).mkv";

    [Test]
    public void Build_ShouldNotThrow_WhenExistingFilesContainDuplicatePaths()
    {
        var existing = CreateExistingFile(id: Guid.NewGuid());
        var duplicate = CreateExistingFile(id: Guid.NewGuid());

        var act = () => LibraryScanDiffBuilder.Build([], [existing, duplicate], [], _ => throw new InvalidOperationException());

        act.Should().NotThrow();
    }

    [Test]
    public void Build_ShouldMarkAllDuplicateExistingAsRemoved_WhenFileIsMissingOnDisk()
    {
        var existing = CreateExistingFile(id: Guid.NewGuid(), created: DateTimeOffset.UtcNow.AddMinutes(-1));
        var duplicate = CreateExistingFile(id: Guid.NewGuid(), created: DateTimeOffset.UtcNow.AddMinutes(-2));

        var diff = LibraryScanDiffBuilder.Build([], [existing, duplicate], [], _ => throw new InvalidOperationException());

        diff.RemovedFiles.Should().HaveCount(2);
        diff.RemovedFiles.Should().Contain(existing);
        diff.RemovedFiles.Should().Contain(duplicate);
    }

    [Test]
    public void Build_ShouldTreatDuplicateScannedPathsAsSingleEntry()
    {
        var scanned = CreateScannedEntry();
        IndexedFile? created = null;

        var diff = LibraryScanDiffBuilder.Build([scanned, scanned with { }], [], [], entry =>
        {
            created = new IndexedFile
            {
                Id = Guid.NewGuid(),
                LibraryId = Guid.NewGuid(),
                Name = entry.Name,
                Extension = entry.Extension,
                Path = entry.Path,
                ParentDirectory = entry.ParentDirectory,
                Hash = 123,
                Size = entry.Size,
                LastWriteTimeUtc = entry.LastWriteTimeUtc
            };
            return created;
        });

        diff.AddedFiles.Should().ContainSingle().Which.Should().BeSameAs(created);
    }

    private static IndexedFile CreateExistingFile(Guid id, DateTimeOffset? created = null) => new()
    {
        Id = id,
        LibraryId = Guid.NewGuid(),
        Name = "A Minecraft Movie (2025)",
        Extension = ".mkv",
        Path = FilePath,
        ParentDirectory = "A Minecraft Movie (2025)",
        Hash = 42,
        Size = 1000,
        LastWriteTimeUtc = DateTimeOffset.UtcNow,
        Created = created ?? DateTimeOffset.UtcNow
    };

    private static ScannedFileEntry CreateScannedEntry() => new()
    {
        Path = FilePath,
        Name = "A Minecraft Movie (2025)",
        Extension = ".mkv",
        ParentDirectory = "A Minecraft Movie (2025)",
        Size = 1000,
        LastWriteTimeUtc = DateTimeOffset.UtcNow
    };
}

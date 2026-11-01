using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.UnitTests.Helpers;

public class LibraryScanRemovalGuardTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "k7-removal-guard", "library");

    [Test]
    public void Apply_ShouldKeepEveryFile_WhenFullScanFindsNoFiles()
    {
        var files = new[] { CreateFile("Movie A", "a.mkv"), CreateFile("Movie B", "b.mkv") };

        var decision = LibraryScanRemovalGuard.Apply(files, Root, isFullScan: true, scannedFileCount: 0, libraryRootAvailable: true, []);

        decision.FilesToRemove.Should().BeEmpty();
        decision.ProtectedFiles.Should().BeEquivalentTo(files);
        decision.BlockReason.Should().Be(LibraryScanRemovalBlockReason.NoFilesFound);
    }

    [Test]
    public void Apply_ShouldKeepEveryFile_WhenLibraryRootIsUnavailable()
    {
        var files = new[] { CreateFile("Movie A", "a.mkv") };

        var decision = LibraryScanRemovalGuard.Apply(files, Root, isFullScan: false, scannedFileCount: 0, libraryRootAvailable: false, []);

        decision.FilesToRemove.Should().BeEmpty();
        decision.ProtectedFiles.Should().BeEquivalentTo(files);
        decision.BlockReason.Should().Be(LibraryScanRemovalBlockReason.LibraryRootUnavailable);
    }

    [Test]
    public void Apply_ShouldRemoveFiles_WhenScopedScanOfDeletedFolderFindsNothing()
    {
        var files = new[] { CreateFile("Movie A", "a.mkv") };
        var missingFolder = Path.Combine(Root, "Movie A");

        var decision = LibraryScanRemovalGuard.Apply(
            files, Root, isFullScan: false, scannedFileCount: 0, libraryRootAvailable: true,
            [(missingFolder, FileInfoHelper.PathDoesNotExistError)]);

        decision.FilesToRemove.Should().BeEquivalentTo(files);
        decision.ProtectedFiles.Should().BeEmpty();
        decision.BlockReason.Should().Be(LibraryScanRemovalBlockReason.None);
    }

    [Test]
    public void Apply_ShouldProtectOnlyFilesUnderUnreadableFolder_WhenOtherFilesAreGone()
    {
        var unreadable = CreateFile("Movie A", "a.mkv");
        var deleted = CreateFile("Movie B", "b.mkv");

        var decision = LibraryScanRemovalGuard.Apply(
            [unreadable, deleted], Root, isFullScan: true, scannedFileCount: 10, libraryRootAvailable: true,
            [(Path.Combine(Root, "Movie A"), "Access to the path is denied.")]);

        decision.FilesToRemove.Should().ContainSingle().Which.Should().BeSameAs(deleted);
        decision.ProtectedFiles.Should().ContainSingle().Which.Should().BeSameAs(unreadable);
    }

    [Test]
    public void Apply_ShouldRemoveFile_WhenOnlyASiblingFolderWithTheSamePrefixIsUnreadable()
    {
        var file = CreateFile("Movie 2", "m.mkv");

        var decision = LibraryScanRemovalGuard.Apply(
            [file], Root, isFullScan: true, scannedFileCount: 10, libraryRootAvailable: true,
            [(Path.Combine(Root, "Movie"), "Timed out after 30s.")]);

        decision.FilesToRemove.Should().ContainSingle().Which.Should().BeSameAs(file);
    }

    [Test]
    public void Apply_ShouldProtectFile_WhenIndexedPathIsRelativeToTheRoot()
    {
        var file = CreateFile("Movie A", "a.mkv");
        file.Path = Path.Combine("Movie A", "a.mkv");

        var decision = LibraryScanRemovalGuard.Apply(
            [file], Root, isFullScan: true, scannedFileCount: 10, libraryRootAvailable: true,
            [(Path.Combine(Root, "Movie A"), "I/O error.")]);

        decision.ProtectedFiles.Should().ContainSingle().Which.Should().BeSameAs(file);
    }

    [Test]
    public void IsDirectoryPopulated_ShouldReflectWhetherTheDirectoryListsEntries()
    {
        var directory = Path.Combine(Path.GetTempPath(), "k7-removal-guard", Guid.NewGuid().ToString("N"));
        try
        {
            FileInfoHelper.IsDirectoryPopulated(directory).Should().BeFalse();

            Directory.CreateDirectory(directory);
            FileInfoHelper.IsDirectoryPopulated(directory).Should().BeFalse();

            File.WriteAllText(Path.Combine(directory, "movie.mkv"), string.Empty);
            FileInfoHelper.IsDirectoryPopulated(directory).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static IndexedFile CreateFile(string folder, string fileName) => new()
    {
        Id = Guid.NewGuid(),
        LibraryId = Guid.NewGuid(),
        Name = Path.GetFileNameWithoutExtension(fileName),
        Extension = Path.GetExtension(fileName),
        Path = Path.Combine(Root, folder, fileName),
        ParentDirectory = folder,
        Hash = 42,
        Size = 1000,
        LastWriteTimeUtc = DateTimeOffset.UtcNow,
        Created = DateTimeOffset.UtcNow
    };
}

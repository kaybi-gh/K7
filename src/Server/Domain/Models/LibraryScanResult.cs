namespace K7.Server.Domain.Models;

public record LibraryScanResult(
    int UnchangedCount,
    int AddedCount,
    int RemovedCount,
    int RenamedCount,
    int SkippedCount,
    IReadOnlyList<string> SkippedFilePaths,
    IReadOnlyList<(string Path, string Error)> InaccessiblePaths);

namespace K7.Server.Domain.ValueObjects;

public record LibraryScanResult(
    int UnchangedCount,
    int AddedCount,
    int RemovedCount,
    int RenamedCount,
    int SkippedCount,
    IReadOnlyList<string> SkippedFilePaths);

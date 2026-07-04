using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Services;

public sealed class LibraryScanProgressReporter(ILibraryNotifier libraryNotifier) : ILibraryScanProgressReporter
{
    public Task ReportProgressAsync(Guid libraryId, int processed, int total, string phase, CancellationToken cancellationToken = default)
        => libraryNotifier.NotifyLibraryScanProgressAsync(libraryId, processed, total, phase, cancellationToken);
}

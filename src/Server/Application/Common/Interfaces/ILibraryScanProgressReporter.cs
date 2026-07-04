namespace K7.Server.Application.Common.Interfaces;

public interface ILibraryScanProgressReporter
{
    Task ReportProgressAsync(Guid libraryId, int processed, int total, string phase, CancellationToken cancellationToken = default);
}

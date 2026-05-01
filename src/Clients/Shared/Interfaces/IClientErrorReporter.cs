namespace K7.Clients.Shared.Interfaces;

public interface IClientErrorReporter
{
    void ReportError(Exception exception, string? context = null);
}

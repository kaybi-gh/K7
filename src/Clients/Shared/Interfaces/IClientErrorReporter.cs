namespace K7.Clients.Shared.Interfaces;

public interface IClientErrorReporter
{
    /// <summary>
    /// Reports an exception to the server (best-effort) and, unless <paramref name="notifyUser"/>
    /// is <c>false</c>, surfaces a user-facing notification (e.g. a snackbar).
    /// Pass <c>notifyUser: false</c> for background/noisy errors (e.g. caught interop exceptions)
    /// that should still be recorded server-side without interrupting the user.
    /// </summary>
    void ReportError(Exception exception, string? context = null, bool notifyUser = true);
}

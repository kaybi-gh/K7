using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Layout;

public class K7ErrorBoundary : ErrorBoundary
{
    [Inject] private IClientErrorReporter ErrorReporter { get; set; } = default!;

    private DateTime _lastErrorTime;
    private int _errorCount;

    private static readonly TimeSpan ErrorWindow = TimeSpan.FromSeconds(5);
    private const int MaxAutoRecovers = 2;

    protected override Task OnErrorAsync(Exception exception)
    {
        ErrorReporter.ReportError(exception, "ErrorBoundary");

        var now = DateTime.UtcNow;

        if (now - _lastErrorTime > ErrorWindow)
        {
            _errorCount = 0;
        }

        _lastErrorTime = now;
        _errorCount++;

        if (_errorCount <= MaxAutoRecovers)
        {
            Recover();
        }

        return Task.CompletedTask;
    }
}

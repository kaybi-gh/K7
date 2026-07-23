using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Catches render errors inside a dialog, reports them via the usual client error path
/// (snackbar + server), then closes the dialog so the host stays usable (e.g. on TV).
/// </summary>
public sealed class K7DialogErrorBoundary : ErrorBoundary
{
    [Inject] private IClientErrorReporter ErrorReporter { get; set; } = default!;

    [Parameter] public EventCallback OnDialogError { get; set; }

    protected override async Task OnErrorAsync(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            await InvokeAsync(Recover);
            return;
        }

        try
        {
            ErrorReporter.ReportError(exception, "Dialog");
        }
        catch
        {
            // Best-effort - don't let reporting failure prevent recovery
        }

        if (OnDialogError.HasDelegate)
        {
            try
            {
                await OnDialogError.InvokeAsync();
            }
            catch
            {
                // Dialog may already be closing
            }
        }
    }
}

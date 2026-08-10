namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Best-effort AudioMuse catalogue repair after music Guids are deleted or replaced.
/// Debounces align (sweep) then cleaning so batch rematch/delete does not spam AudioMuse.
/// </summary>
public interface IMusicIntelligenceCatalogReconciler
{
    /// <summary>
    /// Schedules a debounced reconcile. Safe to call from request threads; work runs in the background.
    /// </summary>
    void RequestReconcile();
}

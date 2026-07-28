namespace K7.Server.Domain.Enums;

/// <summary>
/// Provenance of a background task: who or what created it.
/// </summary>
/// <remarks>
/// This is observability, not priority. The scheduler never orders on it directly; it only influences
/// the initial schedule score at enqueue time (a user action gets an interactive boost).
/// </remarks>
public enum BackgroundTaskTriggeredBy
{
    /// <summary>Chained from another task or a domain event.</summary>
    System = 0,

    /// <summary>Explicit user action: manual refresh, reidentify, scan started from the UI, download.</summary>
    User = 1,

    /// <summary>Periodic scan or scheduled refresh.</summary>
    Scheduler = 2,

    /// <summary>Filesystem change picked up by the library folder watcher.</summary>
    Watcher = 3,

    /// <summary>Incoming peer notification or synchronization.</summary>
    Federation = 4,

    /// <summary>Administrative diagnostics fix.</summary>
    Diagnostics = 5
}

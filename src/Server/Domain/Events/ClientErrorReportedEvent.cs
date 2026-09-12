namespace K7.Server.Domain.Events;

/// <summary>
/// Raised when a client posts to /api/diagnostics/client-errors. Published via <c>IDomainEventPublisher</c>.
/// </summary>
public class ClientErrorReportedEvent : BaseEvent
{
    public ClientErrorReportedEvent(
        string message,
        string? source,
        string? stackTrace,
        string? deviceId,
        string? deviceName,
        string userName)
    {
        Message = message;
        Source = source;
        StackTrace = stackTrace;
        DeviceId = deviceId;
        DeviceName = deviceName;
        UserName = userName;
    }

    public string Message { get; }
    public string? Source { get; }
    public string? StackTrace { get; }
    public string? DeviceId { get; }
    public string? DeviceName { get; }
    public string UserName { get; }
}

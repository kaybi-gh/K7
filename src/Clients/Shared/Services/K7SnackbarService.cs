using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Services;

public sealed class K7SnackbarMessage
{
    public required string Message { get; init; }
    public K7Severity Severity { get; init; }
    public string? Title { get; init; }
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class K7SnackbarService : IK7Snackbar
{
    public event Action<K7SnackbarMessage>? OnAdd;

    public void Add(string message, K7Severity severity = K7Severity.Normal, string? title = null)
        => OnAdd?.Invoke(new K7SnackbarMessage { Message = message, Severity = severity, Title = title });
}

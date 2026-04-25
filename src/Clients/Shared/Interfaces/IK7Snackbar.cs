using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;

namespace K7.Clients.Shared.Interfaces;

public interface IK7Snackbar
{
    event Action<K7SnackbarMessage>? OnAdd;

    void Add(string message, K7Severity severity = K7Severity.Normal, string? title = null);
}

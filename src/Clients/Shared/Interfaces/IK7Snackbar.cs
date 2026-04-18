using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IK7Snackbar
{
    void Add(string message, K7Severity severity = K7Severity.Normal, string? title = null);
}

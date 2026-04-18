using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IK7DialogInstance
{
    string Title { get; }
    K7DialogOptions? Options { get; }
    void SetTitle(string title);
    void Close();
    void Close(K7DialogResult result);
    void Cancel();
}

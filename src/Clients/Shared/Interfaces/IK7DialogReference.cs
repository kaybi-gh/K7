using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IK7DialogReference
{
    Task<K7DialogResult> Result { get; }
    void Close(K7DialogResult result);
    void Cancel();
}

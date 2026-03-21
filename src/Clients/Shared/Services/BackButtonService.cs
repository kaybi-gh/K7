namespace K7.Clients.Shared.Services;

public class BackButtonService
{
    private Func<bool>? _handler;

    public void Register(Func<bool> handler) => _handler = handler;
    public void Unregister() => _handler = null;

    public bool HandleBackButton() => _handler?.Invoke() == true;
}

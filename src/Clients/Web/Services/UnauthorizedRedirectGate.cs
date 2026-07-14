namespace K7.Clients.Web.Services;

public sealed class UnauthorizedRedirectGate
{
    private int _redirecting;

    public bool TryEnter() => Interlocked.CompareExchange(ref _redirecting, 1, 0) == 0;

    public void Exit() => Interlocked.Exchange(ref _redirecting, 0);
}

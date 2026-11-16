namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// One share a visitor opened during the current request.
/// </summary>
public sealed record ShareVisitorGrant(Guid ShareId, Guid SessionId);

/// <summary>
/// Tells whether the current request comes from a public share visitor, and which
/// shares that visitor opened. A request that also carries a real account
/// (guest, user, administrator) is never treated as a share visitor.
/// </summary>
public interface IShareVisitorContext
{
    bool IsShareVisitor { get; }

    IReadOnlyList<ShareVisitorGrant> Grants { get; }
}

/// <summary>
/// Default used outside of HTTP requests (background tasks, tests): never a share visitor.
/// </summary>
public sealed class NoShareVisitorContext : IShareVisitorContext
{
    public bool IsShareVisitor => false;

    public IReadOnlyList<ShareVisitorGrant> Grants => [];
}

namespace K7.Server.Application.Common.Interfaces;

public interface IUser // How to put this into domain?
{
    /// <summary>
    /// The ASP.NET Identity user id from the authentication claims.
    /// </summary>
    string? IdentityId { get; }

    /// <summary>
    /// The domain User entity id.
    /// </summary>
    Guid? Id { get; }

    /// <summary>
    /// Active shared profile id from the request (validated), or null when not in a shared session.
    /// </summary>
    Guid? SharedProfileId { get; }

    /// <summary>
    /// Resolves and caches the domain User entity id without blocking a request thread.
    /// Prefer this method from asynchronous handlers.
    /// </summary>
    Task<Guid?> GetIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves and caches the active shared profile id from the request header.
    /// </summary>
    Task<Guid?> GetSharedProfileIdAsync(CancellationToken cancellationToken = default);
}

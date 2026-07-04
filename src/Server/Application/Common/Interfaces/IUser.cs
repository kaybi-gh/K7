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
}

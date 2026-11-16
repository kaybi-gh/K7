namespace K7.Server.Domain.Constants;

public abstract class Roles
{
    public const string Administrator = nameof(Administrator);
    public const string User = nameof(User);
    public const string Guest = nameof(Guest);

    /// <summary>
    /// Anonymous visitor of a public media share. Never part of the regular policies:
    /// only the share endpoints and the stream endpoints accept it.
    /// </summary>
    public const string ShareVisitor = nameof(ShareVisitor);
}

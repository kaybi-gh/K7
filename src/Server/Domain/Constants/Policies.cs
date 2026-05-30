namespace K7.Server.Domain.Constants;

public abstract class Policies
{
    public const string GuestOrAbove = nameof(GuestOrAbove);
    public const string UserOrAbove = nameof(UserOrAbove);
    public const string AdminOnly = nameof(AdminOnly);
    public const string StreamAccess = nameof(StreamAccess);
    public const string PeerAccess = nameof(PeerAccess);
}

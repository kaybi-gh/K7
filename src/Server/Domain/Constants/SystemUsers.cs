namespace K7.Server.Domain.Constants;

public static class SystemUsers
{
    /// <summary>
    /// Technical account every public share visitor acts as. It has no password and
    /// cannot sign in. It only gives share requests a stable user identity.
    /// </summary>
    public const string ShareVisitorUserName = "k7-share-visitor";
}

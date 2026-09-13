namespace K7.Server.Web.Infrastructure;

internal static class ServerFeatureFlagsCache
{
    public const string Key = "server:feature-flags";
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(1);
}

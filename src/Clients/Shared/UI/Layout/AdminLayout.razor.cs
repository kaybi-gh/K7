namespace K7.Clients.Shared.UI.Layout;

public partial class AdminLayout
{
    private static readonly string[] ContentRoutes = ["/admin/libraries", "/admin/library-groups", "/admin/shares"];
    private static readonly string[] MembersRoutes = ["/admin/users", "/admin/devices", "/admin/restrictions", "/admin/authentication"];
    private static readonly string[] ExperienceRoutes =
    [
        "/admin/interface",
        "/admin/home-layout",
        "/admin/navigation",
        "/admin/audio-playback",
        "/admin/video-playback",
        "/admin/advanced",
        "/admin/transcoding",
        "/admin/general"
    ];
    private static readonly string[] ActivityRoutes = ["/admin/playback-history", "/admin/stats"];
    private static readonly string[] SystemRoutes =
        ["/admin/background-tasks", "/admin/diagnostics", "/admin/diagnostics/details"];
    private static readonly string[] IntegrationRoutes = ["/admin/federation", "/admin/notifications", "/admin/scrobbling", "/admin/music-intelligence", "/admin/api-keys"];
}

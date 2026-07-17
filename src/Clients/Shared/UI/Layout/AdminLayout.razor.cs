namespace K7.Clients.Shared.UI.Layout;

public partial class AdminLayout
{
    private static readonly string[] ContentRoutes = ["/admin/libraries", "/admin/library-groups"];
    private static readonly string[] MembersRoutes = ["/admin/users", "/admin/devices", "/admin/restrictions", "/admin/authentication"];
    private static readonly string[] ExperienceRoutes = ["/admin/general", "/admin/home-layout", "/admin/video-playback", "/admin/transcoding", "/admin/audio-playback"];
    private static readonly string[] ActivityRoutes = ["/admin/playback-history", "/admin/stats"];
    private static readonly string[] SystemRoutes = ["/admin/background-tasks", "/admin/diagnostics"];
    private static readonly string[] IntegrationRoutes = ["/admin/federation", "/admin/notifications", "/admin/music-intelligence", "/admin/api-keys"];
}

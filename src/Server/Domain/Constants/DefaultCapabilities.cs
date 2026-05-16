using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Constants;

public static class DefaultCapabilities
{
    private static readonly HashSet<Capability> UserCapabilities =
    [
        Capability.CanRate,
        Capability.CanCreatePlaylist,
        Capability.CanViewStats,
        Capability.CanResumePlayback,
        Capability.CanViewHistory,
        Capability.CanManageDevices,
        Capability.CanModifySettings,
        Capability.CanReportPlaybackProgress,
        Capability.CanTranscode
    ];

    private static readonly HashSet<Capability> AdminCapabilities =
    [
        ..UserCapabilities,
        Capability.CanAccessAdmin,
        Capability.CanManageLibraries,
        Capability.CanManageUsers
    ];

    private static readonly HashSet<Capability> GuestCapabilities = [];

    public static IReadOnlySet<Capability> ForRole(string role) => role switch
    {
        Roles.Administrator => AdminCapabilities,
        Roles.User => UserCapabilities,
        Roles.Guest => GuestCapabilities,
        _ => GuestCapabilities
    };
}

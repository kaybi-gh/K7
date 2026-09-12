using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Notifications.Services;

public static class GlobalNotificationParameters
{
    public static IReadOnlyList<NotificationParameterInfo> All => NotificationParams.Globals;
}

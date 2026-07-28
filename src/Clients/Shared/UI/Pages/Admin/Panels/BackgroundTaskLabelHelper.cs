using K7.Server.Domain.Enums;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public static class BackgroundTaskLabelHelper
{
    public static string GetTaskTypeLabel(IStringLocalizer localizer, string taskName)
    {
        var key = $"TaskType_{taskName}";
        var localized = localizer[key];
        return localized.ResourceNotFound ? taskName : localized.Value;
    }

    public static string GetLaneLabel(IStringLocalizer localizer, BackgroundTaskLane lane) =>
        GetEnumLabel(localizer, "Lane_", lane);

    public static string GetWorkClassLabel(IStringLocalizer localizer, BackgroundTaskWorkClass workClass) =>
        GetEnumLabel(localizer, "WorkClass_", workClass);

    public static string GetTriggeredByLabel(IStringLocalizer localizer, BackgroundTaskTriggeredBy triggeredBy) =>
        GetEnumLabel(localizer, "TriggeredBy_", triggeredBy);

    private static string GetEnumLabel<TEnum>(IStringLocalizer localizer, string prefix, TEnum value)
        where TEnum : struct, Enum
    {
        var localized = localizer[$"{prefix}{value}"];
        return localized.ResourceNotFound ? value.ToString() : localized.Value;
    }
}

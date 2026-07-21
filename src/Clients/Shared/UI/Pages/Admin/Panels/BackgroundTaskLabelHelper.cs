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
}

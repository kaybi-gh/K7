using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;

namespace K7.Clients.Shared.UI.Extensions;

public static class K7DialogServiceExtensions
{
    public static async Task<bool?> ShowMessageBoxAsync(
        this IK7DialogService service,
        string title,
        string message,
        string yesText = "OK",
        string? noText = null,
        string? cancelText = null)
    {
        var parameters = new K7DialogParameters();
        parameters["Message"] = message;
        parameters["YesText"] = yesText;
        parameters["NoText"] = noText;
        parameters["CancelText"] = cancelText;

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await service.ShowAsync<K7MessageBoxDialog>(title, parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
            return null;

        return result.Data as bool?;
    }
}

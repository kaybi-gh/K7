using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7SettingsActionBar
{
    [Parameter] public bool IsDirty { get; set; }
    [Parameter] public bool IsSaving { get; set; }
    [Parameter] public bool ResetDisabled { get; set; }
    [Parameter] public bool ShowResetToDefaults { get; set; } = true;
    [Parameter] public bool ResetRequiresConfirmation { get; set; } = true;
    [Parameter] public EventCallback OnSave { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public EventCallback OnResetToDefaults { get; set; }

    private async Task ResetToDefaultsAsync()
    {
        if (IsSaving)
            return;

        if (ResetRequiresConfirmation)
        {
            var confirmed = await DialogService.ShowMessageBoxAsync(
                S["ResetToDefaultsTitle"],
                S["ResetToDefaultsMessage"],
                yesText: S["ResetToDefaults"],
                cancelText: S["Cancel"]);

            if (confirmed is not true)
                return;
        }

        await OnResetToDefaults.InvokeAsync();
    }
}

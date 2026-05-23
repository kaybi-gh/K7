using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminNotificationsPanel
{
    [Inject] private INotificationAdminService NotificationService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool _isLoading = true;
    private List<NotificationRuleDto> _rules = [];
    private List<NotificationEventDescriptorDto> _availableEvents = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _rules = await NotificationService.GetNotificationRulesAsync();
            _availableEvents = await NotificationService.GetAvailableEventsAsync();
        }
        catch
        {
            _rules = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string GetEventDisplayName(string eventTypeName)
    {
        var descriptor = _availableEvents.FirstOrDefault(e => e.EventTypeName == eventTypeName);
        return descriptor?.DisplayName ?? eventTypeName;
    }

    private async Task OnToggleEnabled(NotificationRuleDto rule, bool enabled)
    {
        try
        {
            await NotificationService.UpdateNotificationRuleAsync(rule.Id, new UpdateNotificationRuleRequest
            {
                Name = rule.Name,
                ProviderType = rule.ProviderType,
                EventTypeName = rule.EventTypeName,
                ProviderConfig = rule.ProviderConfig,
                PayloadTemplate = rule.PayloadTemplate,
                Conditions = rule.Conditions,
                ConditionsLogic = rule.ConditionsLogic,
                IsEnabled = enabled
            });
            Snackbar.Add(L["RuleUpdated"], K7Severity.Success);
            await LoadData();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task TestRule(NotificationRuleDto rule)
    {
        try
        {
            var success = await NotificationService.TestNotificationRuleAsync(rule.Id);
            Snackbar.Add(success ? L["TestSuccess"] : L["TestFailed"], success ? K7Severity.Success : K7Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task OpenCreateDialog()
    {
        var parameters = new K7DialogParameters<EditNotificationRuleDialog>
        {
            { x => x.AvailableEvents, _availableEvents }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<EditNotificationRuleDialog>(L["CreateRule"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(L["RuleCreated"], K7Severity.Success);
            await LoadData();
        }
    }

    private async Task OpenEditDialog(NotificationRuleDto rule)
    {
        var parameters = new K7DialogParameters<EditNotificationRuleDialog>
        {
            { x => x.ExistingRule, rule },
            { x => x.AvailableEvents, _availableEvents }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<EditNotificationRuleDialog>(L["EditRule"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(L["RuleUpdated"], K7Severity.Success);
            await LoadData();
        }
    }

    private async Task ConfirmDelete(NotificationRuleDto rule)
    {
        var parameters = new K7DialogParameters<ConfirmDeleteNotificationRuleDialog>
        {
            { x => x.DisplayName, rule.Name }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteNotificationRuleDialog>(L["ConfirmDeleteTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                await NotificationService.DeleteNotificationRuleAsync(rule.Id);
                Snackbar.Add(L["RuleDeleted"], K7Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }
}

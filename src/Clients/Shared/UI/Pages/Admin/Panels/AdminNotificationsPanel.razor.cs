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

    private string GetEventDisplayNames(IReadOnlyList<string> eventTypeNames)
    {
        var names = eventTypeNames
            .Select(n => _availableEvents.FirstOrDefault(e => e.EventTypeName == n)?.DisplayName ?? n)
            .Take(3);
        var result = string.Join(", ", names);
        if (eventTypeNames.Count > 3)
            result += $" (+{eventTypeNames.Count - 3})";
        return result;
    }

    private async Task OnToggleEnabled(NotificationRuleDto rule, bool enabled)
    {
        try
        {
            await NotificationService.UpdateNotificationRuleAsync(rule.Id, new UpdateNotificationRuleRequest
            {
                Name = rule.Name,
                ProviderType = rule.ProviderType,
                PayloadFormat = rule.PayloadFormat,
                EventTypeNames = rule.EventTypeNames,
                ProviderConfig = rule.ProviderConfig,
                TitleTemplate = rule.TitleTemplate,
                BodyTemplate = rule.BodyTemplate,
                RawJsonTemplate = rule.RawJsonTemplate,
                RuleFilter = rule.RuleFilter,
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
            var result = await NotificationService.TestNotificationRuleAsync(rule.Id);
            if (result.Success)
            {
                Snackbar.Add(L["TestSuccess"], K7Severity.Success);
            }
            else
            {
                Snackbar.Add(string.Format(L["TestFailedWithError"], result.Error ?? "Unknown error"), K7Severity.Error);
            }
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
            StateHasChanged();
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
            StateHasChanged();
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
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }
}

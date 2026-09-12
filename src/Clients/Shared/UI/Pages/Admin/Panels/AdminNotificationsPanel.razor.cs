using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminNotificationsPanel : IAsyncDisposable
{
    [Inject] private INotificationAdminService NotificationService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<EditNotificationRuleDialog> EventL { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    private bool _isLoading = true;
    private List<NotificationRuleDto> _rules = [];
    private List<NotificationEventDescriptorDto> _availableEvents = [];
    private K7DataTable<NotificationRuleDto>? _tableRef;
    private bool _selectionMode;
    private bool _deleting;
    private readonly HashSet<Guid> _selectedIds = [];
    private SelectionModeKeyboardBinder? _selectionKeys;

    private int SelectedCount => _selectedIds.Count;
    private bool AllSelected => _rules.Count > 0 && _selectedIds.Count == _rules.Count;

    protected override async Task OnInitializedAsync()
    {
        _selectionKeys = new SelectionModeKeyboardBinder(
            SpatialNav,
            onEscape: () => _ = InvokeAsync(OnSelectionEscape),
            onSelectAll: () => _ = InvokeAsync(OnSelectionSelectAll));

        await LoadData();
    }

    public async ValueTask DisposeAsync()
    {
        if (_selectionKeys is not null)
            await _selectionKeys.DisposeAsync();
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
            .Select(LocalizeEventTypeName)
            .Take(3);
        var result = string.Join(", ", names);
        if (eventTypeNames.Count > 3)
            result += $" (+{eventTypeNames.Count - 3})";
        return result;
    }

    private string LocalizeEventTypeName(string eventTypeName)
    {
        var descriptor = _availableEvents.FirstOrDefault(e => e.EventTypeName == eventTypeName);
        var key = descriptor is null
            ? "Event" + eventTypeName
            : (string.IsNullOrWhiteSpace(descriptor.DisplayNameKey)
                ? descriptor.DisplayName
                : descriptor.DisplayNameKey);

        var localized = EventL[key];
        if (!localized.ResourceNotFound)
            return localized.Value;

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
                PayloadFormat = rule.PayloadFormat,
                EventTypeNames = rule.EventTypeNames,
                ProviderConfig = rule.ProviderConfig,
                TitleTemplate = rule.TitleTemplate,
                BodyTemplate = rule.BodyTemplate,
                RawJsonTemplate = rule.RawJsonTemplate,
                RuleFilter = rule.RuleFilter,
                ScheduleWindows = rule.ScheduleWindows,
                CooldownSeconds = rule.CooldownSeconds,
                IsEnabled = enabled
            });
            Snackbar.Add(enabled ? L["NotificationEnabled"] : L["NotificationDisabled"], K7Severity.Success);
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

    private void EnterSelectionMode()
    {
        _selectionMode = true;
        _selectedIds.Clear();
        _tableRef?.InvalidateLayout();
        _ = _selectionKeys?.SetEnabledAsync(true);
    }

    private void ExitSelectionMode()
    {
        if (!_selectionMode)
            return;

        _selectionMode = false;
        _selectedIds.Clear();
        _tableRef?.InvalidateLayout();
        _ = _selectionKeys?.SetEnabledAsync(false);
    }

    private void ToggleSelection(Guid id)
    {
        if (!_selectedIds.Remove(id))
            _selectedIds.Add(id);

        _tableRef?.Rerender();
    }

    private void ToggleSelectAll()
    {
        if (AllSelected)
            _selectedIds.Clear();
        else
            SelectAll();

        _tableRef?.Rerender();
    }

    private void SelectAll()
    {
        _selectedIds.Clear();
        foreach (var rule in _rules)
            _selectedIds.Add(rule.Id);
    }

    private void OnSelectionEscape()
    {
        if (_deleting)
            return;

        ExitSelectionMode();
    }

    private void OnSelectionSelectAll()
    {
        if (!_selectionMode || _deleting)
            return;

        SelectAll();
        _tableRef?.Rerender();
    }

    private bool IsSelected(Guid id) => _selectedIds.Contains(id);

    private void OnRuleRowClick(NotificationRuleDto rule)
    {
        if (_selectionMode)
            ToggleSelection(rule.Id);
    }

    private async Task ConfirmDeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0 || _deleting)
            return;

        var count = _selectedIds.Count;
        var result = await DialogService.ShowMessageBoxAsync(
            L["DeleteSelectedTitle"],
            string.Format(L["DeleteSelectedMessage"], count),
            yesText: S["Delete"],
            cancelText: S["Cancel"]);

        if (result != true)
            return;

        _deleting = true;
        var failed = 0;

        try
        {
            foreach (var id in _selectedIds.ToList())
            {
                try
                {
                    await NotificationService.DeleteNotificationRuleAsync(id);
                }
                catch
                {
                    failed++;
                }
            }
        }
        finally
        {
            _deleting = false;
        }

        ExitSelectionMode();
        await LoadData();

        if (failed == 0)
            Snackbar.Add(string.Format(L["DeleteSelectedSuccess"], count), K7Severity.Success);
        else if (failed == count)
            Snackbar.Add(L["DeleteSelectedError"], K7Severity.Error);
        else
            Snackbar.Add(string.Format(L["DeleteSelectedPartial"], count - failed, failed), K7Severity.Warning);
    }
}

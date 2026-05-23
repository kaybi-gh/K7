using System.Text.Json;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class EditNotificationRuleDialog
{
    [Inject] private INotificationAdminService NotificationService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;
    [Parameter] public NotificationRuleDto? ExistingRule { get; set; }
    [Parameter] public List<NotificationEventDescriptorDto> AvailableEvents { get; set; } = [];

    private string _name = "";
    private string _providerType = "Webhook";
    private string _eventTypeName = "";
    private string _webhookUrl = "";
    private string _webhookMethod = "POST";
    private string _payloadTemplate = "";
    private string _conditions = "";
    private string _conditionsLogic = "";
    private bool _isSubmitting;
    private bool _isEditMode;
    private NotificationEventDescriptorDto? _selectedEvent;

    private bool IsValid => !string.IsNullOrWhiteSpace(_name)
        && !string.IsNullOrWhiteSpace(_eventTypeName)
        && !string.IsNullOrWhiteSpace(_webhookUrl);

    protected override void OnParametersSet()
    {
        if (ExistingRule is not null)
        {
            _isEditMode = true;
            _name = ExistingRule.Name;
            _providerType = ExistingRule.ProviderType;
            _eventTypeName = ExistingRule.EventTypeName;
            _payloadTemplate = ExistingRule.PayloadTemplate ?? "";
            _conditions = ExistingRule.Conditions ?? "";
            _conditionsLogic = ExistingRule.ConditionsLogic ?? "";

            ParseProviderConfig(ExistingRule.ProviderConfig);
        }

        UpdateSelectedEvent();
    }

    private void ParseProviderConfig(string configJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("url", out var urlProp))
                _webhookUrl = urlProp.GetString() ?? "";
            if (root.TryGetProperty("method", out var methodProp))
                _webhookMethod = methodProp.GetString() ?? "POST";
        }
        catch
        {
            _webhookUrl = "";
            _webhookMethod = "POST";
        }
    }

    private string BuildProviderConfig()
    {
        var config = new { url = _webhookUrl, method = _webhookMethod };
        return JsonSerializer.Serialize(config);
    }

    private void OnEventChanged(string value)
    {
        _eventTypeName = value;
        UpdateSelectedEvent();
    }

    private void UpdateSelectedEvent()
    {
        _selectedEvent = AvailableEvents.FirstOrDefault(e => e.EventTypeName == _eventTypeName);
    }

    private void InsertParam(string paramName)
    {
        _payloadTemplate += "{{" + paramName + "}}";
    }

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        if (!IsValid) return;

        _isSubmitting = true;
        try
        {
            var providerConfig = BuildProviderConfig();

            if (_isEditMode && ExistingRule is not null)
            {
                await NotificationService.UpdateNotificationRuleAsync(ExistingRule.Id, new UpdateNotificationRuleRequest
                {
                    Name = _name.Trim(),
                    ProviderType = _providerType,
                    EventTypeName = _eventTypeName,
                    ProviderConfig = providerConfig,
                    PayloadTemplate = string.IsNullOrWhiteSpace(_payloadTemplate) ? null : _payloadTemplate,
                    Conditions = string.IsNullOrWhiteSpace(_conditions) ? null : _conditions,
                    ConditionsLogic = string.IsNullOrWhiteSpace(_conditionsLogic) ? null : _conditionsLogic,
                    IsEnabled = ExistingRule.IsEnabled
                });
            }
            else
            {
                await NotificationService.CreateNotificationRuleAsync(new CreateNotificationRuleRequest
                {
                    Name = _name.Trim(),
                    ProviderType = _providerType,
                    EventTypeName = _eventTypeName,
                    ProviderConfig = providerConfig,
                    PayloadTemplate = string.IsNullOrWhiteSpace(_payloadTemplate) ? null : _payloadTemplate,
                    Conditions = string.IsNullOrWhiteSpace(_conditions) ? null : _conditions,
                    ConditionsLogic = string.IsNullOrWhiteSpace(_conditionsLogic) ? null : _conditionsLogic
                });
            }

            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}

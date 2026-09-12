using System.Text.Json;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Rules;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class EditNotificationRuleDialog
{
    [Inject] private INotificationAdminService NotificationService { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;
    [Parameter] public NotificationRuleDto? ExistingRule { get; set; }
    [Parameter] public List<NotificationEventDescriptorDto> AvailableEvents { get; set; } = [];

    private int _activeStep;
    private string? _selectedCategory;
    private string _name = "";
    private string _providerType = "Webhook";
    private string _payloadFormat = "Structured";
    private string _webhookUrl = "";
    private string _webhookMethod = "POST";
    private Dictionary<string, string> _webhookHeaders = [];
    private string _titleTemplate = "";
    private string _bodyTemplate = "";
    private string _rawJsonTemplate = "";
    private RuleGroupDto? _ruleFilter;
    private bool _isSubmitting;
    private bool _isEditMode;
    private bool _isPreviewMode;
    private int? _cooldownSeconds;
    private string _selectedPresetId = "";
    private List<NotificationWebhookPresetDto> _presets = [];
    private readonly List<ScheduleWindowEdit> _scheduleWindows = [];
    private readonly HashSet<string> _selectedEventNames = [];
    private K7TextField<string>? _titleField;
    private K7TextField<string>? _bodyField;
    private K7TextField<string>? _rawField;
    private TemplateField _activeTemplateField = TemplateField.Body;

    private enum TemplateField
    {
        Title,
        Body,
        Raw
    }

    private static readonly IReadOnlyList<ButtonGroupOption<bool>> _previewModeOptions =
    [
        new(false, Icon: Phosphor.PencilSimple),
        new(true, Icon: Phosphor.Eye)
    ];

    private bool IsValid => !string.IsNullOrWhiteSpace(_name)
        && _selectedEventNames.Count > 0
        && IsHttpOrHttpsWebhookUrl(_webhookUrl);

    private List<string> AvailableCategories =>
        AvailableEvents
            .Select(e => e.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

    private List<NotificationEventDescriptorDto> EventsForSelectedCategory =>
        AvailableEvents
            .Where(e => e.Category == _selectedCategory)
            .OrderBy(e => LocalizeEvent(e))
            .ToList();

    private List<NotificationParameterInfoDto> AvailableParameters =>
        AvailableEvents
            .Where(e => _selectedEventNames.Contains(e.EventTypeName))
            .SelectMany(e => e.Parameters)
            .DistinctBy(p => p.Name)
            .ToList();

    private IReadOnlyList<K7GroupedListGroup<NotificationParameterInfoDto>> ParameterGroups =>
        AvailableParameters
            .GroupBy(p => p.Group)
            .OrderBy(g => g.Key)
            .Select(g => new K7GroupedListGroup<NotificationParameterInfoDto>
            {
                Key = g.Key,
                Label = LocalizeGroup(g.Key),
                Items = g.OrderBy(p => LocalizeParam(p)).ToList()
            })
            .ToList();

    private bool HasDefaultTemplates =>
        AvailableEvents.Any(e => _selectedEventNames.Contains(e.EventTypeName)
            && !string.IsNullOrWhiteSpace(e.DefaultTitleTemplate));

    private bool HasPreset => !string.IsNullOrWhiteSpace(_selectedPresetId);

    private string? WebhookUrlHelperText
    {
        get
        {
            if (!HasPreset)
                return null;

            var preset = _presets.FirstOrDefault(p => p.Id == _selectedPresetId);
            if (preset is null)
                return null;

            var localized = L[preset.DisplayNameKey + "UrlHelper"];
            return localized.ResourceNotFound ? null : localized.Value;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _presets = await NotificationService.GetWebhookPresetsAsync();
        }
        catch
        {
            _presets = [];
        }
    }

    protected override void OnParametersSet()
    {
        if (ExistingRule is not null)
        {
            _isEditMode = true;
            _activeStep = 0;
            _maxVisitedStep = 2;
            _name = ExistingRule.Name;
            _providerType = ExistingRule.ProviderType;
            _payloadFormat = ExistingRule.PayloadFormat;
            _titleTemplate = ExistingRule.TitleTemplate ?? "";
            _bodyTemplate = ExistingRule.BodyTemplate ?? "";
            _rawJsonTemplate = ExistingRule.RawJsonTemplate ?? "";
            _ruleFilter = ExistingRule.RuleFilter;
            _cooldownSeconds = ExistingRule.CooldownSeconds;

            _selectedEventNames.Clear();
            foreach (var evt in ExistingRule.EventTypeNames)
                _selectedEventNames.Add(evt);

            ParseProviderConfig(ExistingRule.ProviderConfig);
            LoadScheduleWindows(ExistingRule.ScheduleWindows);

            var firstEvent = AvailableEvents.FirstOrDefault(e => ExistingRule.EventTypeNames.Contains(e.EventTypeName));
            _selectedCategory = firstEvent?.Category;
        }
    }

    private int _maxVisitedStep;
    private int LastStepIndex => _isEditMode ? 2 : 4;
    private int ContentStep => _isEditMode ? _activeStep + 2 : _activeStep;

    private IReadOnlyList<string> _stepLabels => _isEditMode
        ? [L["StepDestination"].Value, L["StepConditions"].Value, L["StepMessage"].Value]
        : [
            L["StepCategory"].Value,
            L["StepEvents"].Value,
            L["StepDestination"].Value,
            L["StepConditions"].Value,
            L["StepMessage"].Value
        ];

    private bool CanAdvance() => ContentStep switch
    {
        0 => _selectedCategory is not null,
        1 => _selectedEventNames.Count > 0,
        2 => !string.IsNullOrWhiteSpace(_name) && IsHttpOrHttpsWebhookUrl(_webhookUrl),
        3 => true,
        4 => _payloadFormat == "Structured"
            ? !string.IsNullOrWhiteSpace(_titleTemplate) || !string.IsNullOrWhiteSpace(_bodyTemplate)
            : !string.IsNullOrWhiteSpace(_rawJsonTemplate) && IsValidJsonTemplate(_rawJsonTemplate),
        _ => true
    };

    private static bool IsValidJsonTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return false;

        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            template,
            @"\{\{.+?\}\}",
            "x",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        try
        {
            JsonDocument.Parse(sanitized);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string? ValidateRawJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return IsValidJsonTemplate(value) ? null : (string)L["InvalidJson"];
    }

    private string? ValidateWebhookUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return IsHttpOrHttpsWebhookUrl(value) ? null : (string)L["WebhookUrlInvalid"];
    }

    private static bool IsHttpOrHttpsWebhookUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private void GoToStep(int step)
    {
        if (step <= _maxVisitedStep)
        {
            _activeStep = step;
            EnsureDefaultTemplatesIfNeeded();
        }
    }

    private void NextStep()
    {
        if (CanAdvance() && _activeStep < LastStepIndex)
        {
            _activeStep++;
            if (_activeStep > _maxVisitedStep)
                _maxVisitedStep = _activeStep;
            EnsureDefaultTemplatesIfNeeded();
        }
    }

    private void PreviousStep()
    {
        if (_activeStep > 0)
            _activeStep--;
    }

    /// <summary>
    /// Prefill event defaults on the Message step when templates are still empty
    /// (create flow, or edit with blank templates and no webhook preset).
    /// </summary>
    private void EnsureDefaultTemplatesIfNeeded()
    {
        if (ContentStep != 4 || HasPreset)
            return;

        if (!string.IsNullOrWhiteSpace(_titleTemplate)
            || !string.IsNullOrWhiteSpace(_bodyTemplate)
            || !string.IsNullOrWhiteSpace(_rawJsonTemplate))
        {
            return;
        }

        ApplyDefaultTemplates();
    }

    private void SelectCategory(string category)
    {
        if (_selectedCategory != category)
        {
            _selectedCategory = category;
            _selectedEventNames.Clear();
        }
    }

    private static readonly Dictionary<string, object> InitialFocusAttributes = new()
    {
        ["data-initial-focus"] = true
    };

    private void OnCategoryKeyDown(KeyboardEventArgs e, string category)
    {
        if (e.Key is "Enter" or " ")
            SelectCategory(category);
    }

    private string GetCategoryEventCount(string category)
    {
        var count = AvailableEvents.Count(e => e.Category == category);
        return string.Format(L["EventCount"], count);
    }

    private string GetCategoryCardClass(string category)
    {
        return _selectedCategory == category ? "k7-paper--selected focusable" : "focusable";
    }

    private string GetCategoryColor(string category)
    {
        return _selectedCategory == category ? "primary" : "default";
    }

    private string GetCategoryIcon(string category) => category switch
    {
        "Playback" => Phosphor.PlayCircle,
        "Library" => Phosphor.Books,
        "Media" => Phosphor.FilmStrip,
        "Playlist" => Phosphor.Playlist,
        "Device" => Phosphor.Desktop,
        "Download" => Phosphor.Download,
        "System" => Phosphor.Gear,
        "Federation" => Phosphor.Globe,
        "Health" => Phosphor.Heartbeat,
        "User" => Phosphor.User,
        "Security" => Phosphor.Shield,
        _ => Phosphor.Bell
    };

    private string GetCategoryLabel(string category) => category switch
    {
        "Playback" => L["CategoryPlayback"],
        "Library" => L["CategoryLibrary"],
        "Media" => L["CategoryMedia"],
        "Playlist" => L["CategoryPlaylist"],
        "Device" => L["CategoryDevice"],
        "Download" => L["CategoryDownload"],
        "System" => L["CategorySystem"],
        "Federation" => L["CategoryFederation"],
        "Health" => L["CategoryHealth"],
        "User" => L["CategoryUser"],
        "Security" => L["CategorySecurity"],
        _ => category
    };

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
            if (root.TryGetProperty("headers", out var headersProp) && headersProp.ValueKind == JsonValueKind.Object)
            {
                _webhookHeaders = headersProp.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _webhookUrl = "";
            _webhookMethod = "POST";
            _webhookHeaders = [];
        }
    }

    private string BuildProviderConfig()
    {
        var config = new
        {
            url = _webhookUrl,
            method = _webhookMethod,
            headers = _webhookHeaders.Count == 0 ? null : _webhookHeaders
        };
        return JsonSerializer.Serialize(config);
    }

    private bool IsEventSelected(string eventTypeName) => _selectedEventNames.Contains(eventTypeName);

    private void OnEventToggled(string eventTypeName, bool selected)
    {
        if (selected)
            _selectedEventNames.Add(eventTypeName);
        else
            _selectedEventNames.Remove(eventTypeName);
    }

    private void ApplyDefaultTemplates()
    {
        var firstSelected = AvailableEvents
            .FirstOrDefault(e => _selectedEventNames.Contains(e.EventTypeName));

        if (firstSelected is null) return;

        _titleTemplate = firstSelected.DefaultTitleTemplate;
        _bodyTemplate = firstSelected.DefaultBodyTemplate;
        if (_payloadFormat == "RawJson" && string.IsNullOrWhiteSpace(_selectedPresetId))
            SyncRawFromStructured();
    }

    private void SetPayloadFormat(string format)
    {
        if (HasPreset)
            return;

        if (string.Equals(_payloadFormat, format, StringComparison.Ordinal))
            return;

        if (format == "RawJson")
            SyncRawFromStructured();
        else
            SyncStructuredFromRaw();

        _payloadFormat = format;
    }

    private void SyncRawFromStructured()
    {
        _rawJsonTemplate = JsonSerializer.Serialize(
            new Dictionary<string, string>
            {
                ["title"] = _titleTemplate ?? "",
                ["body"] = _bodyTemplate ?? ""
            },
            new JsonSerializerOptions { WriteIndented = true });
    }

    private void SyncStructuredFromRaw()
    {
        if (string.IsNullOrWhiteSpace(_rawJsonTemplate))
            return;

        try
        {
            using var doc = JsonDocument.Parse(_rawJsonTemplate);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            if (doc.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String)
                _titleTemplate = title.GetString() ?? _titleTemplate;

            if (doc.RootElement.TryGetProperty("body", out var body)
                && body.ValueKind == JsonValueKind.String)
            {
                _bodyTemplate = body.GetString() ?? _bodyTemplate;
            }
            else if (doc.RootElement.TryGetProperty("message", out var message)
                     && message.ValueKind == JsonValueKind.String)
            {
                _bodyTemplate = message.GetString() ?? _bodyTemplate;
            }
        }
        catch (JsonException)
        {
            // Keep current structured fields when raw JSON is not a simple title/body object.
        }
    }

    private async Task InsertParam(string paramName)
    {
        var token = "{{" + paramName + "}}";
        var field = ResolveActiveTemplateField();
        if (field is not null)
        {
            await field.InsertAtCursorAsync(token);
            return;
        }

        if (_payloadFormat == "RawJson")
            _rawJsonTemplate += token;
        else if (_activeTemplateField == TemplateField.Title)
            _titleTemplate += token;
        else
            _bodyTemplate += token;
    }

    private K7TextField<string>? ResolveActiveTemplateField()
    {
        if (_payloadFormat == "RawJson")
            return _rawField;

        return _activeTemplateField == TemplateField.Title ? _titleField : _bodyField;
    }

    private static string FormatParamDescription(NotificationParameterInfoDto param)
    {
        var token = "{{" + param.Name + "}}";
        var sample = string.IsNullOrWhiteSpace(param.SampleValue) ? "" : param.SampleValue;
        if (param.FilterOptions is not { Count: > 0 })
            return string.IsNullOrEmpty(sample) ? token : $"{token}  {sample}";

        var values = string.Join(" | ", param.FilterOptions.Select(o => o.Value));
        return string.IsNullOrEmpty(sample)
            ? $"{token}\n{values}"
            : $"{token}  {sample}\n{values}";
    }

    private static IEnumerable<string> ParamKeywords(NotificationParameterInfoDto param)
    {
        yield return param.Name;
        if (!string.IsNullOrWhiteSpace(param.SampleValue))
            yield return param.SampleValue;

        if (param.FilterOptions is null)
            yield break;

        foreach (var option in param.FilterOptions)
        {
            if (!string.IsNullOrWhiteSpace(option.Value))
                yield return option.Value;
            if (!string.IsNullOrWhiteSpace(option.Label))
                yield return option.Label;
        }
    }

    private string LocalizeEvent(NotificationEventDescriptorDto evt)
    {
        var key = string.IsNullOrWhiteSpace(evt.DisplayNameKey) ? evt.DisplayName : evt.DisplayNameKey;
        var localized = L[key];
        return localized.ResourceNotFound ? evt.DisplayName : localized.Value;
    }

    private string LocalizeParam(NotificationParameterInfoDto param)
    {
        var key = string.IsNullOrWhiteSpace(param.DisplayNameKey)
            ? "Param" + param.Name.Replace(".", "", StringComparison.Ordinal)
            : param.DisplayNameKey;
        var localized = Fields[key];
        return localized.ResourceNotFound ? param.Name : localized.Value;
    }

    private string LocalizeGroup(string group)
    {
        var localized = Fields["Group" + group];
        return localized.ResourceNotFound ? group : localized.Value;
    }

    private string LocalizePreset(NotificationWebhookPresetDto preset)
    {
        var localized = L[preset.DisplayNameKey];
        return localized.ResourceNotFound ? preset.Id : localized.Value;
    }

    private string LocalizeDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => L["DayMon"],
        DayOfWeek.Tuesday => L["DayTue"],
        DayOfWeek.Wednesday => L["DayWed"],
        DayOfWeek.Thursday => L["DayThu"],
        DayOfWeek.Friday => L["DayFri"],
        DayOfWeek.Saturday => L["DaySat"],
        DayOfWeek.Sunday => L["DaySun"],
        _ => day.ToString()
    };

    private void OnPresetChanged(string? id)
    {
        _selectedPresetId = id ?? "";
        if (string.IsNullOrWhiteSpace(_selectedPresetId))
        {
            _payloadFormat = "Structured";
            if (string.IsNullOrWhiteSpace(_titleTemplate) && string.IsNullOrWhiteSpace(_bodyTemplate))
                ApplyDefaultTemplates();
            return;
        }

        var preset = _presets.FirstOrDefault(p => p.Id == _selectedPresetId);
        if (preset is null)
            return;

        // Always apply the preset URL scaffold on selection so Discord -> Slack (etc.) updates the field.
        // Placeholders (... , <token>, your-topic) stay until the user replaces them.
        _webhookUrl = preset.UrlHint;

        _webhookMethod = preset.Method;
        _webhookHeaders = new Dictionary<string, string>(preset.Headers, StringComparer.OrdinalIgnoreCase);
        _payloadFormat = "RawJson";
        _rawJsonTemplate = preset.RawJsonTemplate;
    }

    private IReadOnlyList<RuleFieldDescriptorDto> GetConditionFieldDescriptors()
    {
        return AvailableParameters.Select(p =>
        {
            var valueType = Enum.TryParse<RuleFieldValueType>(p.FilterValueType, out var parsed)
                ? parsed
                : RuleFieldValueType.Text;

            var options = p.FilterOptions?.Select(o => o with
            {
                Label = LocalizeOption(o.Label)
            }).ToList();

            return new RuleFieldDescriptorDto
            {
                FieldName = p.Name,
                DisplayName = LocalizeParam(p),
                ValueType = valueType,
                Group = LocalizeGroup(p.Group),
                Options = options,
                Operators = OperatorsFor(valueType)
            };
        }).ToList();
    }

    private string LocalizeOption(string label)
    {
        if (label is "true")
            return S["Yes"];
        if (label is "false")
            return S["No"];
        return label;
    }

    private static IReadOnlyList<RuleOperator> OperatorsFor(RuleFieldValueType type) => type switch
    {
        RuleFieldValueType.Number =>
        [
            RuleOperator.Equals, RuleOperator.NotEquals,
            RuleOperator.GreaterThan, RuleOperator.LessThan,
            RuleOperator.GreaterThanOrEqual, RuleOperator.LessThanOrEqual,
            RuleOperator.IsEmpty, RuleOperator.IsNotEmpty
        ],
        RuleFieldValueType.Date =>
        [
            RuleOperator.Equals, RuleOperator.NotEquals,
            RuleOperator.GreaterThan, RuleOperator.LessThan,
            RuleOperator.GreaterThanOrEqual, RuleOperator.LessThanOrEqual,
            RuleOperator.InLast, RuleOperator.IsEmpty, RuleOperator.IsNotEmpty
        ],
        RuleFieldValueType.Boolean or RuleFieldValueType.Select or RuleFieldValueType.Language =>
        [
            RuleOperator.Equals, RuleOperator.NotEquals
        ],
        RuleFieldValueType.Search =>
        [
            RuleOperator.Equals, RuleOperator.NotEquals,
            RuleOperator.Contains, RuleOperator.NotContains
        ],
        _ =>
        [
            RuleOperator.Equals, RuleOperator.NotEquals,
            RuleOperator.Contains, RuleOperator.NotContains,
            RuleOperator.BeginsWith, RuleOperator.EndsWith,
            RuleOperator.IsEmpty, RuleOperator.IsNotEmpty
        ]
    };

    private async Task<IReadOnlyList<string>> SearchConditionSuggestionsAsync(
        string field,
        string text,
        CancellationToken cancellationToken)
    {
        if (field is "User.Name" or "UserName")
        {
            var users = await UserAdminService.GetUsersAsync(cancellationToken);
            return users
                .Select(u => u.DisplayName ?? u.UserName ?? "")
                .Where(n => n.Contains(text, StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();
        }

        if (field is "Library.Title" or "LibraryTitle")
        {
            var libraries = await LibraryService.GetLibrariesAsync(cancellationToken);
            return libraries
                .Select(l => l.Title)
                .Where(n => n.Contains(text, StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();
        }

        return [];
    }

    private void LoadScheduleWindows(IReadOnlyList<NotificationScheduleWindowDto> windows)
    {
        _scheduleWindows.Clear();
        foreach (var window in windows)
        {
            _scheduleWindows.Add(new ScheduleWindowEdit
            {
                Days = window.Days.ToList(),
                Start = window.Start,
                End = window.End
            });
        }
    }

    private void AddScheduleWindow()
    {
        _scheduleWindows.Add(new ScheduleWindowEdit());
    }

    private void RemoveScheduleWindow(int index)
    {
        if (index >= 0 && index < _scheduleWindows.Count)
            _scheduleWindows.RemoveAt(index);
    }

    private static void ToggleScheduleDay(ScheduleWindowEdit window, DayOfWeek day)
    {
        var value = (int)day;
        if (!window.Days.Remove(value))
            window.Days.Add(value);
    }

    private void Cancel() => Dialog.Cancel();

    private string RenderPreview(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return "";

        return System.Text.RegularExpressions.Regex.Replace(
            template,
            @"\{\{(.+?)\}\}",
            match =>
            {
                var expression = match.Groups[1].Value.Trim();
                var parts = expression.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return match.Value;

                var key = parts[0];
                var param = AvailableParameters.FirstOrDefault(p =>
                    string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
                var sample = param is null || string.IsNullOrWhiteSpace(param.SampleValue)
                    ? key
                    : param.SampleValue;

                if (parts.Length == 1)
                    return sample;

                string? fallback = null;
                for (var i = 1; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var eq = part.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    var mapKey = part[..eq].Trim();
                    var mapValue = part[(eq + 1)..];
                    if (mapKey == "*")
                    {
                        fallback = mapValue;
                        continue;
                    }

                    if (string.Equals(mapKey, sample, StringComparison.OrdinalIgnoreCase))
                        return mapValue;
                }

                return fallback ?? sample;
            },
            System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    private async Task Submit()
    {
        if (!IsValid) return;

        _isSubmitting = true;
        try
        {
            var providerConfig = BuildProviderConfig();
            var windows = _scheduleWindows.Select(w => new NotificationScheduleWindowDto
            {
                Days = w.Days,
                Start = w.Start,
                End = w.End
            }).ToList();

            if (_isEditMode && ExistingRule is not null)
            {
                await NotificationService.UpdateNotificationRuleAsync(ExistingRule.Id, new UpdateNotificationRuleRequest
                {
                    Name = _name.Trim(),
                    ProviderType = _providerType,
                    PayloadFormat = _payloadFormat,
                    EventTypeNames = _selectedEventNames.ToList(),
                    ProviderConfig = providerConfig,
                    TitleTemplate = string.IsNullOrWhiteSpace(_titleTemplate) ? null : _titleTemplate,
                    BodyTemplate = string.IsNullOrWhiteSpace(_bodyTemplate) ? null : _bodyTemplate,
                    RawJsonTemplate = string.IsNullOrWhiteSpace(_rawJsonTemplate) ? null : _rawJsonTemplate,
                    RuleFilter = _ruleFilter,
                    ScheduleWindows = windows,
                    CooldownSeconds = _cooldownSeconds,
                    IsEnabled = ExistingRule.IsEnabled
                });
            }
            else
            {
                await NotificationService.CreateNotificationRuleAsync(new CreateNotificationRuleRequest
                {
                    Name = _name.Trim(),
                    ProviderType = _providerType,
                    PayloadFormat = _payloadFormat,
                    EventTypeNames = _selectedEventNames.ToList(),
                    ProviderConfig = providerConfig,
                    TitleTemplate = string.IsNullOrWhiteSpace(_titleTemplate) ? null : _titleTemplate,
                    BodyTemplate = string.IsNullOrWhiteSpace(_bodyTemplate) ? null : _bodyTemplate,
                    RawJsonTemplate = string.IsNullOrWhiteSpace(_rawJsonTemplate) ? null : _rawJsonTemplate,
                    RuleFilter = _ruleFilter,
                    ScheduleWindows = windows,
                    CooldownSeconds = _cooldownSeconds
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

    private sealed class ScheduleWindowEdit
    {
        public List<int> Days { get; set; } = [];
        public string Start { get; set; } = "08:00";
        public string End { get; set; } = "22:00";
    }
}

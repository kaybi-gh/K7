using System.Text.Json;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Rules;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class EditNotificationRuleDialog
{
    [Inject] private INotificationAdminService NotificationService { get; set; } = default!;
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
    private string _titleTemplate = "";
    private string _bodyTemplate = "";
    private string _rawJsonTemplate = "";
    private RuleGroupDto? _ruleFilter;
    private bool _isSubmitting;
    private bool _isEditMode;
    private bool _isPreviewMode;
    private readonly HashSet<string> _selectedEventNames = [];

    private static readonly IReadOnlyList<ButtonGroupOption<bool>> _previewModeOptions =
    [
        new(false, Icon: Phosphor.PencilSimple),
        new(true, Icon: Phosphor.Eye)
    ];

    private bool IsValid => !string.IsNullOrWhiteSpace(_name)
        && _selectedEventNames.Count > 0
        && !string.IsNullOrWhiteSpace(_webhookUrl);

    private List<string> AvailableCategories =>
        AvailableEvents
            .Select(e => e.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

    private List<NotificationEventDescriptorDto> EventsForSelectedCategory =>
        AvailableEvents
            .Where(e => e.Category == _selectedCategory)
            .OrderBy(e => e.DisplayName)
            .ToList();

    private List<NotificationParameterInfoDto> AvailableParameters =>
        AvailableEvents
            .Where(e => _selectedEventNames.Contains(e.EventTypeName))
            .SelectMany(e => e.Parameters)
            .DistinctBy(p => p.Name)
            .OrderBy(p => p.Name)
            .ToList();

    private List<NotificationParameterInfoDto> EventParameters =>
        AvailableParameters.Where(p => !IsGlobalParam(p.Name)).ToList();

    private List<NotificationParameterInfoDto> GlobalParameters =>
        AvailableParameters.Where(p => IsGlobalParam(p.Name)).ToList();

    private static bool IsGlobalParam(string name) =>
        name is "EventType" || name.StartsWith("Current.", StringComparison.Ordinal) || name.StartsWith("Server.", StringComparison.Ordinal);

    private bool HasDefaultTemplates =>
        AvailableEvents.Any(e => _selectedEventNames.Contains(e.EventTypeName)
            && !string.IsNullOrWhiteSpace(e.DefaultTitleTemplate));

    protected override void OnParametersSet()
    {
        if (ExistingRule is not null)
        {
            _isEditMode = true;
            _maxVisitedStep = 3;
            _name = ExistingRule.Name;
            _providerType = ExistingRule.ProviderType;
            _payloadFormat = ExistingRule.PayloadFormat;
            _titleTemplate = ExistingRule.TitleTemplate ?? "";
            _bodyTemplate = ExistingRule.BodyTemplate ?? "";
            _rawJsonTemplate = ExistingRule.RawJsonTemplate ?? "";
            _ruleFilter = ExistingRule.RuleFilter;

            _selectedEventNames.Clear();
            foreach (var evt in ExistingRule.EventTypeNames)
            {
                _selectedEventNames.Add(evt);
            }

            ParseProviderConfig(ExistingRule.ProviderConfig);

            var firstEvent = AvailableEvents.FirstOrDefault(e => ExistingRule.EventTypeNames.Contains(e.EventTypeName));
            _selectedCategory = firstEvent?.Category;
        }
    }

    private int _maxVisitedStep;

    private bool CanAdvance() => _activeStep switch
    {
        0 => _selectedCategory is not null,
        1 => _selectedEventNames.Count > 0,
        2 => _payloadFormat == "Structured"
            ? !string.IsNullOrWhiteSpace(_titleTemplate) || !string.IsNullOrWhiteSpace(_bodyTemplate)
            : !string.IsNullOrWhiteSpace(_rawJsonTemplate) && IsValidJsonTemplate(_rawJsonTemplate),
        _ => true
    };

    private static bool IsValidJsonTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return false;

        var sanitized = System.Text.RegularExpressions.Regex.Replace(template, @"\{\{[^}]+\}\}", "\"__placeholder__\"");

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

    private bool CanGoToStep(int step) => step <= _maxVisitedStep;

    private void GoToStep(int step)
    {
        if (step <= _maxVisitedStep)
            _activeStep = step;
    }

    private void NextStep()
    {
        if (CanAdvance() && _activeStep < 3)
        {
            _activeStep++;
            if (_activeStep > _maxVisitedStep)
                _maxVisitedStep = _activeStep;
        }
    }

    private void PreviousStep()
    {
        if (_activeStep > 0)
            _activeStep--;
    }

    private void SelectCategory(string category)
    {
        if (_selectedCategory != category)
        {
            _selectedCategory = category;
            _selectedEventNames.Clear();
        }
    }

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
    }

    private void InsertParam(string paramName)
    {
        var token = "{{" + paramName + "}}";
        if (_payloadFormat == "RawJson")
            _rawJsonTemplate += token;
        else
            _bodyTemplate += token;
    }

    private string GetParamTooltip(NotificationParameterInfoDto param)
    {
        var sample = GetSampleValue(param.Name, param.ValueType);
        return $"{sample}\n{param.DisplayName} ({param.ValueType})";
    }

    private static string GetSampleValue(string name, string valueType)
    {
        return name switch
        {
            "EventType" => "MediaCreatedEvent",
            "Server.Name" => "K7",
            "Server.Url" => "https://k7.example.com",
            "Server.Version" => "1.0.0",
            "Current.Year" => "2026",
            "Current.Month" => "5",
            "Current.Day" => "24",
            "Current.Hour" => "14",
            "Current.Minute" => "30",
            "Current.Weekday" => "Saturday",
            "Current.Datestamp" => "2026-05-24",
            "Current.Timestamp" => "2026-05-24T14:30:00Z",
            "Media.Title" => "Interstellar",
            "Media.OriginalTitle" => "Interstellar",
            "Media.MediaType" => "Movie",
            "Media.ReleaseYear" => "2014",
            "Media.Genres.Count" => "3",
            "Media.Pictures.Count" => "2",
            "Media.IndexedFiles.Count" => "1",
            "PictureUrl" => "https://k7.example.com/api/metadata-pictures/a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            "BackdropUrl" => "https://k7.example.com/api/metadata-pictures/f9e8d7c6-b5a4-3210-fedc-ba9876543210",
            "Library.Title" => "Films",
            "Library.MediaType" => "Movies",
            "Library.MetadataProviderName" => "TMDB",
            "Library.MetadataLanguage" => "fr",
            "Device.Name" => "Galaxy S24",
            "Device.OperatingSystemVersion" => "Android 15",
            "Device.DisplayWidth" => "1080",
            "Device.DisplayHeight" => "2340",
            "Device.DeviceUniqueId" => "abc123",
            "Device.LastSeen" => "2026-05-24T12:00:00Z",
            "Playlist.Title" => "Road Trip",
            "Playlist.Description" => "Songs for the road",
            "Playlist.Items.Count" => "42",
            "IndexedFile.FileName" => "interstellar.mkv",
            "IndexedFile.ParentDirectory" => "/media/movies",
            "IndexedFile.Size" => "4200000000",
            "Collection.Title" => "Marvel",
            "Collection.Description" => "MCU movies",
            "Collection.Items.Count" => "33",
            "SmartPlaylist.Title" => "Recently Added",
            "SmartPlaylist.Description" => "Last 30 days",
            "SmartPlaylist.OrderDirection" => "Descending",
            "Download.IndexedFileId" => "a1b2c3d4-...",
            "Download.DeviceId" => "e5f6g7h8-...",
            "Download.UserId" => "i9j0k1l2-...",
            _ => valueType switch
            {
                "Int32" or "Int64" => "42",
                "Guid" => "a1b2c3d4-...",
                "DateTime" => "2026-05-24T12:00:00Z",
                "Boolean" => "true",
                _ => "..."
            }
        };
    }

    private IReadOnlyList<RuleFieldDescriptorDto> GetConditionFieldDescriptors()
    {
        return AvailableParameters.Select(p => new RuleFieldDescriptorDto
        {
            FieldName = p.Name,
            DisplayName = p.DisplayName,
            ValueType = p.ValueType switch
            {
                "Int32" or "Int64" => RuleFieldValueType.Number,
                "Boolean" => RuleFieldValueType.Boolean,
                "DateTime" => RuleFieldValueType.Date,
                _ => RuleFieldValueType.Text
            },
            Operators =
            [
                RuleOperator.Equals,
                RuleOperator.NotEquals,
                RuleOperator.Contains,
                RuleOperator.NotContains,
                RuleOperator.GreaterThan,
                RuleOperator.LessThan,
                RuleOperator.BeginsWith,
                RuleOperator.EndsWith,
                RuleOperator.IsEmpty,
                RuleOperator.IsNotEmpty
            ]
        }).ToList();
    }

    private void Cancel() => Dialog.Cancel();

    private string RenderPreview(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return "";

        var result = template;
        foreach (var param in AvailableParameters)
        {
            var token = "{{" + param.Name + "}}";
            if (result.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                var sample = GetSampleValue(param.Name, param.ValueType);
                result = result.Replace(token, sample, StringComparison.OrdinalIgnoreCase);
            }
        }
        return result;
    }

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
                    PayloadFormat = _payloadFormat,
                    EventTypeNames = _selectedEventNames.ToList(),
                    ProviderConfig = providerConfig,
                    TitleTemplate = string.IsNullOrWhiteSpace(_titleTemplate) ? null : _titleTemplate,
                    BodyTemplate = string.IsNullOrWhiteSpace(_bodyTemplate) ? null : _bodyTemplate,
                    RawJsonTemplate = string.IsNullOrWhiteSpace(_rawJsonTemplate) ? null : _rawJsonTemplate,
                    RuleFilter = _ruleFilter,
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
                    RuleFilter = _ruleFilter
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

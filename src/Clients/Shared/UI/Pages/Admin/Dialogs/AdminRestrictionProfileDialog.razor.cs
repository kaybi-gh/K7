using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class AdminRestrictionProfileDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public bool IsNew { get; set; } = true;
    [Parameter] public Guid? ProfileId { get; set; }
    [Parameter] public string? InitialName { get; set; }
    [Parameter] public string? InitialDescription { get; set; }
    [Parameter] public RuleGroupDto? InitialRuleFilter { get; set; }

    private string _name = "";
    private string? _description;
    private RuleGroupDto _ruleFilter = new() { MatchCondition = RuleMatchCondition.Any, Items = [] };
    private IReadOnlyList<RuleFieldDescriptorDto> _fieldDescriptors = [];
    private MediaTagsDto? _tags;
    private bool _saving;

    protected override async Task OnInitializedAsync()
    {
        _name = InitialName ?? "";
        _description = InitialDescription;
        _ruleFilter = InitialRuleFilter ?? new RuleGroupDto { MatchCondition = RuleMatchCondition.Any, Items = [] };

        await LoadTagsAsync();
        RefreshFieldDescriptors();
    }

    private async Task LoadTagsAsync()
    {
        try
        {
            _tags = await MediaService.GetMediaTagsAsync(new GetMediaTagsQuery());
        }
        catch
        {
            _tags = null;
        }
    }

    private void RefreshFieldDescriptors()
    {
        _fieldDescriptors = RuleFieldLocalization.Localize(
            RuleFieldCatalog.GetAllDescriptors(),
            SpL,
            BrowseL,
            _tags);
    }

    private void OnRuleFilterChanged(RuleGroupDto value) => _ruleFilter = value;

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        _saving = true;
        try
        {
            if (IsNew)
            {
                await K7ServerService.CreateContentRestrictionProfileAsync(new CreateContentRestrictionProfileRequest
                {
                    Name = _name.Trim(),
                    Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                    RuleFilter = _ruleFilter
                });
                Snackbar.Add(L["Created"], K7Severity.Success);
            }
            else
            {
                await K7ServerService.UpdateContentRestrictionProfileAsync(ProfileId!.Value, new UpdateContentRestrictionProfileRequest
                {
                    Name = _name.Trim(),
                    Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                    RuleFilter = _ruleFilter
                });
                Snackbar.Add(L["Updated"], K7Severity.Success);
            }

            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(L["SaveError"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task<IReadOnlyList<string>> SearchSuggestionsAsync(
        string field,
        string searchText,
        CancellationToken cancellationToken) =>
        await MediaBrowseTagSearch.SearchAsync(
            MediaService,
            field,
            searchText,
            libraryIds: null,
            libraryGroupIds: null,
            mediaType: default,
            cancellationToken);
}

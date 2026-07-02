using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminRestrictionsPanel
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SmartPlaylistDialog> SpL { get; set; } = default!;
    [Inject] private IStringLocalizer<LibraryBrowseFilters> BrowseL { get; set; } = default!;

    private bool _loading = true;
    private List<ContentRestrictionProfileDto> _profiles = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _loading = true;
        try
        {
            _profiles = await K7ServerService.GetContentRestrictionProfilesAsync();
        }
        catch
        {
            _profiles = [];
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OpenCreateDialog()
    {
        var parameters = new K7DialogParameters<AdminRestrictionProfileDialog>
        {
            { x => x.IsNew, true }
        };
        await ShowDialog(parameters);
    }

    private async Task OpenEditDialog(ContentRestrictionProfileDto profile)
    {
        var parameters = new K7DialogParameters<AdminRestrictionProfileDialog>
        {
            { x => x.IsNew, false },
            { x => x.ProfileId, profile.Id },
            { x => x.InitialName, profile.Name },
            { x => x.InitialDescription, profile.Description },
            { x => x.InitialRuleFilter, profile.RuleFilter }
        };
        await ShowDialog(parameters);
    }

    private async Task CreateFromTemplate(TemplateDefinition template)
    {
        var parameters = new K7DialogParameters<AdminRestrictionProfileDialog>
        {
            { x => x.IsNew, true },
            { x => x.InitialName, L[template.Name] },
            { x => x.InitialDescription, L[template.Description] },
            { x => x.InitialRuleFilter, BuildTemplateRuleFilter(template) }
        };
        await ShowDialog(parameters);
    }

    private static RuleGroupDto BuildTemplateRuleFilter(TemplateDefinition template) => new()
    {
        MatchCondition = template.MatchCondition == RestrictionMatchCondition.All
            ? RuleMatchCondition.All
            : RuleMatchCondition.Any,
        Items = template.Rules.Select<TemplateRule, RuleGroupItemDto>(r => new ConditionRuleItemDto
        {
            Field = r.Field.ToString(),
            Operator = MapToRuleOperator(r.Operator),
            Value = r.Value
        }).ToList()
    };

    private async Task ShowDialog(K7DialogParameters<AdminRestrictionProfileDialog> parameters)
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AdminRestrictionProfileDialog>(L["ProfileTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadData();
    }

    private async Task PreviewRestrictedMedias(ContentRestrictionProfileDto profile)
    {
        var parameters = new K7DialogParameters<AdminRestrictedMediasDialog>
        {
            { x => x.ProfileId, profile.Id },
            { x => x.ProfileName, profile.Name }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AdminRestrictedMediasDialog>(L["RestrictedMediaTitle"], parameters, options);
    }

    private async Task ConfirmDelete(ContentRestrictionProfileDto profile)
    {
        var parameters = new K7DialogParameters<ConfirmDeleteUserDialog>
        {
            { x => x.DisplayName, profile.Name }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteUserDialog>(L["DeleteProfileTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                await K7ServerService.DeleteContentRestrictionProfileAsync(profile.Id);
                Snackbar.Add(L["ProfileDeleted"], K7Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }

    private string FormatRule(ConditionRuleItemDto rule)
    {
        var field = RuleFieldLocalization.GetFieldLabel(rule.Field, SpL, BrowseL);
        var op = rule.Operator switch
        {
            RuleOperator.Equals => "=",
            RuleOperator.NotEquals => "!=",
            RuleOperator.Contains => L["OperatorContains"].Value,
            RuleOperator.NotContains => L["OperatorNotContains"].Value,
            RuleOperator.GreaterThan => ">",
            RuleOperator.LessThan => "<",
            RuleOperator.GreaterThanOrEqual => ">=",
            RuleOperator.LessThanOrEqual => "<=",
            RuleOperator.IsEmpty => L["OperatorIsEmpty"].Value,
            RuleOperator.IsNotEmpty => L["OperatorIsNotEmpty"].Value,
            _ => rule.Operator.ToString()
        };
        if (rule.Operator is RuleOperator.IsEmpty or RuleOperator.IsNotEmpty)
            return $"{field} {op}";
        return $"{field} {op} {rule.Value}";
    }

    public sealed record TemplateDefinition(string Name, string Description, RestrictionMatchCondition MatchCondition, List<TemplateRule> Rules);

    public sealed record TemplateRule(RestrictionField Field, RestrictionOperator Operator, string? Value = null);

    public static class Templates
    {
        public static readonly TemplateDefinition ChildFriendly = new(
            "TemplateChildFriendly",
            "TemplateChildFriendlyDesc",
            RestrictionMatchCondition.Any,
            [
                new(RestrictionField.ContentRating, RestrictionOperator.IsEmpty),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "12"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "16"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "18"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "PG-13"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "R"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "NC-17"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "TV-14"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "TV-MA"),
                new(RestrictionField.Genre, RestrictionOperator.Equals, "Horreur"),
                new(RestrictionField.Genre, RestrictionOperator.Equals, "Horror"),
                new(RestrictionField.Genre, RestrictionOperator.Equals, "Thriller"),
            ]);

        public static readonly TemplateDefinition Family12 = new(
            "TemplateFamily12",
            "TemplateFamily12Desc",
            RestrictionMatchCondition.Any,
            [
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "16"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "18"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "R"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "NC-17"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "TV-MA"),
            ]);

        public static readonly TemplateDefinition Teen16 = new(
            "TemplateTeen16",
            "TemplateTeen16Desc",
            RestrictionMatchCondition.Any,
            [
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "18"),
                new(RestrictionField.ContentRating, RestrictionOperator.Equals, "NC-17"),
            ]);

        public static readonly TemplateDefinition NoHorror = new(
            "TemplateNoHorror",
            "TemplateNoHorrorDesc",
            RestrictionMatchCondition.Any,
            [
                new(RestrictionField.Genre, RestrictionOperator.Equals, "Horreur"),
                new(RestrictionField.Genre, RestrictionOperator.Equals, "Horror"),
                new(RestrictionField.Genre, RestrictionOperator.Equals, "Thriller"),
            ]);
    }

    private static RuleOperator MapToRuleOperator(RestrictionOperator op) => op switch
    {
        RestrictionOperator.Equals => RuleOperator.Equals,
        RestrictionOperator.NotEquals => RuleOperator.NotEquals,
        RestrictionOperator.Contains => RuleOperator.Contains,
        RestrictionOperator.NotContains => RuleOperator.NotContains,
        RestrictionOperator.GreaterThan => RuleOperator.GreaterThan,
        RestrictionOperator.LessThan => RuleOperator.LessThan,
        RestrictionOperator.GreaterThanOrEqual => RuleOperator.GreaterThanOrEqual,
        RestrictionOperator.LessThanOrEqual => RuleOperator.LessThanOrEqual,
        RestrictionOperator.IsEmpty => RuleOperator.IsEmpty,
        RestrictionOperator.IsNotEmpty => RuleOperator.IsNotEmpty,
        _ => RuleOperator.Equals
    };
}

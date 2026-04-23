using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminRestrictionsPanel
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

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
            { x => x.Name, profile.Name },
            { x => x.Description, profile.Description },
            { x => x.MatchCondition, profile.MatchCondition },
            { x => x.Rules, profile.Rules.Select(r => new AdminRestrictionProfileDialog.RuleEntry
            {
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList() }
        };
        await ShowDialog(parameters);
    }

    private async Task CreateFromTemplate(TemplateDefinition template)
    {
        var parameters = new K7DialogParameters<AdminRestrictionProfileDialog>
        {
            { x => x.IsNew, true },
            { x => x.Name, L[template.Name] },
            { x => x.Description, L[template.Description] },
            { x => x.MatchCondition, template.MatchCondition },
            { x => x.Rules, template.Rules.Select(r => new AdminRestrictionProfileDialog.RuleEntry
            {
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList() }
        };
        await ShowDialog(parameters);
    }

    private async Task ShowDialog(K7DialogParameters<AdminRestrictionProfileDialog> parameters)
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
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

    private string FormatRule(ContentRestrictionRuleDto rule)
    {
        var field = rule.Field switch
        {
            RestrictionField.Genre => L["FieldGenre"].Value,
            RestrictionField.ContentRating => L["FieldContentRating"].Value,
            RestrictionField.ReleaseYear => L["FieldReleaseYear"].Value,
            _ => rule.Field.ToString()
        };
        var op = rule.Operator switch
        {
            RestrictionOperator.Equals => "=",
            RestrictionOperator.NotEquals => "?",
            RestrictionOperator.Contains => L["OperatorContains"].Value,
            RestrictionOperator.NotContains => L["OperatorNotContains"].Value,
            RestrictionOperator.GreaterThan => ">",
            RestrictionOperator.LessThan => "<",
            RestrictionOperator.GreaterThanOrEqual => "=",
            RestrictionOperator.LessThanOrEqual => "=",
            RestrictionOperator.IsEmpty => L["OperatorIsEmpty"].Value,
            RestrictionOperator.IsNotEmpty => L["OperatorIsNotEmpty"].Value,
            _ => rule.Operator.ToString()
        };
        if (rule.Operator is RestrictionOperator.IsEmpty or RestrictionOperator.IsNotEmpty)
            return $"{field} {op}";
        return $"{field} {op} {rule.Value}";
    }

    public sealed record TemplateDefinition(string Name, string Description, RestrictionMatchCondition MatchCondition, List<ContentRestrictionRuleDto> Rules);

    public static class Templates
    {
        public static readonly TemplateDefinition ChildFriendly = new(
            "TemplateChildFriendly",
            "TemplateChildFriendlyDesc",
            RestrictionMatchCondition.Any,
            [
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.IsEmpty },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "12" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "16" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "18" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "PG-13" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "R" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "NC-17" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "TV-14" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "TV-MA" },
                new() { Field = RestrictionField.Genre, Operator = RestrictionOperator.Equals, Value = "Horreur" },
                new() { Field = RestrictionField.Genre, Operator = RestrictionOperator.Equals, Value = "Horror" },
                new() { Field = RestrictionField.Genre, Operator = RestrictionOperator.Equals, Value = "Thriller" },
            ]);

        public static readonly TemplateDefinition Family12 = new(
            "TemplateFamily12",
            "TemplateFamily12Desc",
            RestrictionMatchCondition.Any,
            [
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "16" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "18" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "R" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "NC-17" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "TV-MA" },
            ]);

        public static readonly TemplateDefinition Teen16 = new(
            "TemplateTeen16",
            "TemplateTeen16Desc",
            RestrictionMatchCondition.Any,
            [
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "18" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "NC-17" },
            ]);

        public static readonly TemplateDefinition NoHorror = new(
            "TemplateNoHorror",
            "TemplateNoHorrorDesc",
            RestrictionMatchCondition.Any,
            [
                new() { Field = RestrictionField.Genre, Operator = RestrictionOperator.Equals, Value = "Horreur" },
                new() { Field = RestrictionField.Genre, Operator = RestrictionOperator.Equals, Value = "Horror" },
                new() { Field = RestrictionField.Genre, Operator = RestrictionOperator.Equals, Value = "Thriller" },
            ]);
    }
}

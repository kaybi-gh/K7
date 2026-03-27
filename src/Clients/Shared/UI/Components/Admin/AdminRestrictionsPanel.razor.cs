using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminRestrictionsPanel
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

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
        var parameters = new DialogParameters<AdminRestrictionProfileDialog>
        {
            { x => x.IsNew, true }
        };
        await ShowDialog(parameters);
    }

    private async Task OpenEditDialog(ContentRestrictionProfileDto profile)
    {
        var parameters = new DialogParameters<AdminRestrictionProfileDialog>
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
        var parameters = new DialogParameters<AdminRestrictionProfileDialog>
        {
            { x => x.IsNew, true },
            { x => x.Name, template.Name },
            { x => x.Description, template.Description },
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

    private async Task ShowDialog(DialogParameters<AdminRestrictionProfileDialog> parameters)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AdminRestrictionProfileDialog>("Profil de restriction", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadData();
    }

    private async Task PreviewRestrictedMedias(ContentRestrictionProfileDto profile)
    {
        var parameters = new DialogParameters<AdminRestrictedMediasDialog>
        {
            { x => x.ProfileId, profile.Id },
            { x => x.ProfileName, profile.Name }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AdminRestrictedMediasDialog>("Médias restreints", parameters, options);
    }

    private async Task ConfirmDelete(ContentRestrictionProfileDto profile)
    {
        var parameters = new DialogParameters<ConfirmDeleteUserDialog>
        {
            { x => x.DisplayName, profile.Name }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteUserDialog>("Supprimer le profil", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                await K7ServerService.DeleteContentRestrictionProfileAsync(profile.Id);
                Snackbar.Add("Profil supprimé.", Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Erreur : {ex.Message}", Severity.Error);
            }
        }
    }

    private static string FormatRule(ContentRestrictionRuleDto rule)
    {
        var field = rule.Field switch
        {
            RestrictionField.Genre => "Genre",
            RestrictionField.ContentRating => "Classification",
            RestrictionField.ReleaseYear => "Année",
            _ => rule.Field.ToString()
        };
        var op = rule.Operator switch
        {
            RestrictionOperator.Equals => "=",
            RestrictionOperator.NotEquals => "≠",
            RestrictionOperator.Contains => "contient",
            RestrictionOperator.NotContains => "ne contient pas",
            RestrictionOperator.GreaterThan => ">",
            RestrictionOperator.LessThan => "<",
            RestrictionOperator.GreaterThanOrEqual => "≥",
            RestrictionOperator.LessThanOrEqual => "≤",
            RestrictionOperator.IsEmpty => "est vide",
            RestrictionOperator.IsNotEmpty => "n'est pas vide",
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
            "Tout public (enfants)",
            "Bloque les contenus classifiés 12+ / PG-13+, sans classification, et les genres Horreur/Horror/Thriller",
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
            "Famille (+12)",
            "Bloque les contenus classifiés 16+ / R+",
            RestrictionMatchCondition.Any,
            [
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "16" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "18" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "R" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "NC-17" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "TV-MA" },
            ]);

        public static readonly TemplateDefinition Teen16 = new(
            "Adolescent (+16)",
            "Bloque uniquement les contenus classifiés 18 / NC-17",
            RestrictionMatchCondition.Any,
            [
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "18" },
                new() { Field = RestrictionField.ContentRating, Operator = RestrictionOperator.Equals, Value = "NC-17" },
            ]);

        public static readonly TemplateDefinition NoHorror = new(
            "Sans horreur",
            "Bloque les genres Horreur/Horror et Thriller",
            RestrictionMatchCondition.Any,
            [
                new() { Field = RestrictionField.Genre, Operator = RestrictionOperator.Equals, Value = "Horreur" },
                new() { Field = RestrictionField.Genre, Operator = RestrictionOperator.Equals, Value = "Horror" },
                new() { Field = RestrictionField.Genre, Operator = RestrictionOperator.Equals, Value = "Thriller" },
            ]);
    }
}

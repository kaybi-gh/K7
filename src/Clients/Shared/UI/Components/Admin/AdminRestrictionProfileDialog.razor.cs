using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminRestrictionProfileDialog
{
    [Inject] private IUserAdminService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public bool IsNew { get; set; } = true;
    [Parameter] public Guid? ProfileId { get; set; }
    [Parameter] public string Name { get; set; } = "";
    [Parameter] public string? Description { get; set; }
    [Parameter] public RestrictionMatchCondition MatchCondition { get; set; } = RestrictionMatchCondition.Any;
    [Parameter] public List<RuleEntry> Rules { get; set; } = [];

    private bool _saving;

    private void AddRule()
    {
        Rules.Add(new RuleEntry { Field = RestrictionField.Genre, Operator = RestrictionOperator.Equals });
    }

    private void OnFieldChanged(int index, RestrictionField newField)
    {
        Rules[index].Field = newField;
        var validOps = GetOperatorsForField(newField);
        if (!validOps.Contains(Rules[index].Operator))
            Rules[index].Operator = validOps[0];
    }

    private static List<RestrictionOperator> GetOperatorsForField(RestrictionField field) => field switch
    {
        RestrictionField.Genre or RestrictionField.ContentRating =>
        [
            RestrictionOperator.Equals,
            RestrictionOperator.NotEquals,
            RestrictionOperator.Contains,
            RestrictionOperator.NotContains,
            RestrictionOperator.IsEmpty,
            RestrictionOperator.IsNotEmpty,
        ],
        RestrictionField.ReleaseYear =>
        [
            RestrictionOperator.Equals,
            RestrictionOperator.NotEquals,
            RestrictionOperator.GreaterThan,
            RestrictionOperator.LessThan,
            RestrictionOperator.GreaterThanOrEqual,
            RestrictionOperator.LessThanOrEqual,
            RestrictionOperator.IsEmpty,
            RestrictionOperator.IsNotEmpty,
        ],
        _ => [RestrictionOperator.Equals]
    };

    private static string FormatOperator(RestrictionOperator op) => op switch
    {
        RestrictionOperator.Equals => "est égal à",
        RestrictionOperator.NotEquals => "n'est pas égal à",
        RestrictionOperator.Contains => "contient",
        RestrictionOperator.NotContains => "ne contient pas",
        RestrictionOperator.GreaterThan => "supérieur à",
        RestrictionOperator.LessThan => "inférieur à",
        RestrictionOperator.GreaterThanOrEqual => "supérieur ou égal à",
        RestrictionOperator.LessThanOrEqual => "inférieur ou égal à",
        RestrictionOperator.IsEmpty => "est vide",
        RestrictionOperator.IsNotEmpty => "n'est pas vide",
        _ => op.ToString()
    };

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        _saving = true;
        try
        {
            var rules = Rules.Select(r => new ContentRestrictionRuleRequest
            {
                Field = r.Field,
                Operator = r.Operator,
                Value = r.Value
            }).ToList();

            if (IsNew)
            {
                await K7ServerService.CreateContentRestrictionProfileAsync(new CreateContentRestrictionProfileRequest
                {
                    Name = Name,
                    Description = Description,
                    MatchCondition = MatchCondition,
                    Rules = rules
                });
                Snackbar.Add("Profil créé.", K7Severity.Success);
            }
            else
            {
                await K7ServerService.UpdateContentRestrictionProfileAsync(ProfileId!.Value, new UpdateContentRestrictionProfileRequest
                {
                    Name = Name,
                    Description = Description,
                    MatchCondition = MatchCondition,
                    Rules = rules
                });
                Snackbar.Add("Profil mis à jour.", K7Severity.Success);
            }
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Erreur : {ex.Message}", K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    public class RuleEntry
    {
        public RestrictionField Field { get; set; }
        public RestrictionOperator Operator { get; set; }
        public string? Value { get; set; }
    }
}

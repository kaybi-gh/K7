using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

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
        RestrictionOperator.Equals => "est egal a",
        RestrictionOperator.NotEquals => "n'est pas egal a",
        RestrictionOperator.Contains => "contient",
        RestrictionOperator.NotContains => "ne contient pas",
        RestrictionOperator.GreaterThan => "superieur a",
        RestrictionOperator.LessThan => "inferieur a",
        RestrictionOperator.GreaterThanOrEqual => "superieur ou egal a",
        RestrictionOperator.LessThanOrEqual => "inferieur ou egal a",
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
            var ruleFilter = BuildRuleGroupDto();

            if (IsNew)
            {
                await K7ServerService.CreateContentRestrictionProfileAsync(new CreateContentRestrictionProfileRequest
                {
                    Name = Name,
                    Description = Description,
                    RuleFilter = ruleFilter
                });
                Snackbar.Add("Profil cree.", K7Severity.Success);
            }
            else
            {
                await K7ServerService.UpdateContentRestrictionProfileAsync(ProfileId!.Value, new UpdateContentRestrictionProfileRequest
                {
                    Name = Name,
                    Description = Description,
                    RuleFilter = ruleFilter
                });
                Snackbar.Add("Profil mis a jour.", K7Severity.Success);
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

    private RuleGroupDto BuildRuleGroupDto()
    {
        return new RuleGroupDto
        {
            MatchCondition = MatchCondition == RestrictionMatchCondition.All
                ? RuleMatchCondition.All
                : RuleMatchCondition.Any,
            Items = Rules.Select(r => (RuleGroupItemDto)new ConditionRuleItemDto
            {
                Field = r.Field.ToString(),
                Operator = MapToRuleOperator(r.Operator),
                Value = r.Value
            }).ToList()
        };
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

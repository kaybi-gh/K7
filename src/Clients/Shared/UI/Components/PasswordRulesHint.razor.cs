using K7.Shared.Dtos;
using K7.Shared.Security;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class PasswordRulesHint
{
    [Parameter] public string? Password { get; set; }

    [Parameter] public PasswordPolicyDto Policy { get; set; } = PasswordPolicyDto.Defaults;

    private string LabelFor(PasswordRule rule) => rule switch
    {
        PasswordRule.MinLength => L["RuleMinLength", Policy.RequiredLength],
        PasswordRule.Digit => L["RuleDigit"],
        PasswordRule.Lowercase => L["RuleLowercase"],
        PasswordRule.Uppercase => L["RuleUppercase"],
        PasswordRule.NonAlphanumeric => L["RuleNonAlphanumeric"],
        PasswordRule.UniqueChars => L["RuleUniqueChars", Policy.RequiredUniqueChars],
        _ => rule.ToString()
    };
}

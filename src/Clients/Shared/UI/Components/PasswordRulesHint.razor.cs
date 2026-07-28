using K7.Shared.Security;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class PasswordRulesHint
{
    [Parameter] public string? Password { get; set; }

    private string LabelFor(PasswordRule rule) => rule switch
    {
        PasswordRule.MinLength => L["RuleMinLength", PasswordPolicy.RequiredLength],
        PasswordRule.Digit => L["RuleDigit"],
        PasswordRule.Lowercase => L["RuleLowercase"],
        PasswordRule.Uppercase => L["RuleUppercase"],
        PasswordRule.NonAlphanumeric => L["RuleNonAlphanumeric"],
        PasswordRule.UniqueChars => L["RuleUniqueChars", PasswordPolicy.RequiredUniqueChars],
        _ => rule.ToString()
    };
}

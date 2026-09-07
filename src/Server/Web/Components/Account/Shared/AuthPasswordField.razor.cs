using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Server.Web.Components.Account.Shared;

public partial class AuthPasswordField
{
    [Inject] private IStringLocalizer<AuthPasswordField> L { get; set; } = default!;

    [Parameter] public string Name { get; set; } = "Input.Password";
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public string? Value { get; set; }
    [Parameter] public PasswordPolicyDto Policy { get; set; } = PasswordPolicyDto.Defaults;
    [Parameter] public bool ShowVisibilityToggle { get; set; }
    [Parameter] public string? ToggleLabel { get; set; }
    [Parameter] public bool ShowConfirm { get; set; }
    [Parameter] public string ConfirmName { get; set; } = "Input.ConfirmPassword";
    [Parameter] public string? ConfirmId { get; set; }
    [Parameter] public string? ConfirmLabel { get; set; }
    [Parameter] public string? ConfirmPlaceholder { get; set; }
    [Parameter] public string? ConfirmValue { get; set; }

    private string PasswordId => Id ?? "pw-auth";
    private string ResolvedConfirmId => ConfirmId ?? "pw-auth-confirm";
    private string HintBase => ConfirmName.Replace('.', '-');
    private string MismatchId => $"{HintBase}-mismatch";
    private string MatchId => $"{HintBase}-match";
}

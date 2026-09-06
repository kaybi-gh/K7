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

    private string _password = "";
    private string _confirm = "";
    private bool _reveal;
    private bool _revealConfirm;

    private string InputType => _reveal ? "text" : "password";
    private string ConfirmInputType => _revealConfirm ? "text" : "password";
    private bool ShowMismatch =>
        ShowConfirm && !string.IsNullOrEmpty(_confirm) && !string.Equals(_password, _confirm, StringComparison.Ordinal);
    private bool ShowMatch =>
        ShowConfirm && !string.IsNullOrEmpty(_confirm) && string.Equals(_password, _confirm, StringComparison.Ordinal);
    private string? ConfirmHintId =>
        ShowMismatch || ShowMatch ? $"{ConfirmName.Replace('.', '-')}-hint" : null;

    protected override void OnParametersSet()
    {
        if (string.IsNullOrEmpty(_password) && !string.IsNullOrEmpty(Value))
            _password = Value;
        if (string.IsNullOrEmpty(_confirm) && !string.IsNullOrEmpty(ConfirmValue))
            _confirm = ConfirmValue;
    }

    private void OnPasswordInput(ChangeEventArgs args) =>
        _password = args.Value?.ToString() ?? "";

    private void OnConfirmInput(ChangeEventArgs args) =>
        _confirm = args.Value?.ToString() ?? "";

    private void ToggleVisibility() => _reveal = !_reveal;

    private void ToggleConfirmVisibility() => _revealConfirm = !_revealConfirm;
}

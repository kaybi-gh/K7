using System.ComponentModel.DataAnnotations;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Infrastructure.Configuration;
using K7.Server.Web.Components.Account;
using K7.Shared.Dtos;
using K7.Shared.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;

namespace K7.Server.Web.Components.Pages.Setup;

public partial class SetupWizard
{
    private string? _statusMessage;
    private bool _requiresSetupToken;
    private AuthenticationScheme[] _externalProviders = [];
    private PasswordPolicyDto _policy = PasswordPolicyDto.Defaults;

    [Inject]
    private ISetupTokenProvider SetupTokenProvider { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel? Input { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();
        _policy = await PasswordPolicyService.GetAsync();

        _requiresSetupToken = await SetupService.RequiresSetupTokenAsync();

        if (AuthConfig.Value.Oidc.Enabled)
        {
            _externalProviders = (await SignInManager.GetExternalAuthenticationSchemesAsync()).ToArray();
        }
    }

    private async Task OnSubmitAsync()
    {
        if (await SetupService.IsSetupCompletedAsync())
        {
            RedirectManager.RedirectTo("/");
            return;
        }

        var setupToken = _requiresSetupToken
            ? Input!.SetupToken ?? SetupTokenProvider.CurrentToken
            : null;

        if (!EmailFormat.IsValidOptional(Input!.Email))
        {
            _statusMessage = L["EmailInvalid"];
            return;
        }

        if (!string.Equals(Input.Password, Input.ConfirmPassword, StringComparison.Ordinal))
        {
            _statusMessage = L["PasswordsMismatch"];
            return;
        }

        if (!PasswordPolicy.IsSatisfiedBy(Input.Password, _policy))
        {
            _statusMessage = L["PasswordPolicyNotMet"];
            return;
        }

        var email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim();
        var result = await SetupService.CompleteSetupAsync(Input.Username.Trim(), Input.Password, email, setupToken);

        if (result.Succeeded)
        {
            var user = await UserManager.FindByNameAsync(Input.Username.Trim());
            await SignInManager.SignInAsync(user!, isPersistent: false);
            RedirectManager.RedirectTo("/");
            return;
        }

        _statusMessage = string.Join(" ", result.Errors);
    }

    private sealed class InputModel
    {
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = string.Empty;

        public string? Email
        {
            get;
            set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;

        public string? SetupToken { get; set; }
    }
}
